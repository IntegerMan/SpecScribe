# ADR 0036: The Webview Shell Supplies Chrome Scripts — Amending ADR 0032 §2's Enforcement Clause

**Status:** Proposed (authored 2026-08-01 by `spec-vscode-extension-name-latency-and-webview-sunburst`; ratification is the owner's)
**Date:** 2026-08-01
**Deciders:** Matthew-Hope Eland
**Amends:** [ADR 0032](0032-csp-posture-after-the-projection-layer.md) §Decision 2 (the enforcement clause only — **not** the policy string, and **not** Decision 1)
**Relates to:** [ADR 0005](0005-vs-code-webview-runtime-and-packaging.md) §4 (the CSP, unchanged); [ADR 0012](0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md) §Decision 5 (already discharged by ADR 0032 — this ADR does **not** re-discharge it); [ADR 0013](0013-text-twin-is-the-no-js-contract.md) §Decision 7 (the webview text-twin fallback this retires as the *primary* presentation); [ADR 0024](0024-spa-and-webview-are-filtered-projections-of-one-region-seam.md) (the region seam the webview consumes); [ADR 0030](0030-epic-24-graph-engine.md) (its "decided once, for both" requirement); [ADR 0031](0031-text-twin-standardization-moves-to-its-own-epic.md) (why no twin audit is owed here)

## Context

Hierarchy and graph charts do not render on the VS Code webview surface. On the dashboard the reader gets a panel heading and a legend describing a chart that is not there: `.ss-hierarchy` is `display:none` until `[data-hierarchy-ready]`, the boot placeholder is gated behind a `:root[data-ss-hierarchy-boot]` attribute the webview never sets, and the dashboard's text twin is `sr-only`. Three independent legs are severed:

1. `WebviewRenderAdapter.StripDataIslands` deletes the component's only data source.
2. The Explorer's mount code (`specscribe.js`) is not shipped — the registered `asset.js` host exception.
3. The 1.22 MB engine `<script src>` is emitted only by `HtmlRenderAdapter.Render`, which this adapter never calls.

**None of this was ever a CSP limit, and the codebase already said so.** The `hierarchy-chart` entry in `HostRenderExceptions.Registry` describes its own cause as *"a SEQUENCING choice rather than a technical limit,"* noting the Story 20.4 spike proved Plotly renders under the shipped policy. ADR 0030 §Good states flatly: *"The webview CSP needs no relaxation. `script-src 'nonce-…'` alone suffices."*

**ADR 0032 already decided the governing principle** and therefore already discharged ADR 0012 §Decision 5's "land it once, not twice." Its §Decision 2 says every chrome script is *"emitted outside the region by `HtmlRenderAdapter.Render`, and therefore **replaced by whichever shell consumes the region**"* — and its §Decision 3 says *"a nonce'd or shell-supplied `<script src>` in the head is exactly what `script-src 'nonce-…'` is for."* The webview shell simply never supplied the replacements. **This ADR is not a second CSP decision; writing one would be the duplication ADR 0012 §5 forbids.** It records one narrow consequence of doing what ADR 0032 already permits.

## Decision

**1. The webview shell supplies the chrome scripts the region depends on, under the existing nonce.** `WebviewRenderAdapter`'s document shell emits the vendored chart engine and the Explorer's mount code as nonce'd `<script>` blocks, once per document. **No change to the CSP policy string**: `default-src 'none'`, `script-src 'nonce-…'`, no `'unsafe-inline'`, no `'strict-dynamic'`, `localResourceRoots` stays empty. ADR 0032 §Decision 1 is untouched and this ADR must not be read as softening it.

**2. The mount logic must not be forked.** Whether the shell ships the whole `specscribe.js` or a scoped entry point is an implementation choice; a *second copy* of the Explorer's mount logic is not. Divergent per-surface reimplementations of shared chart behavior is the exact `convention-not-component` failure ADR 0012 exists to end (three hand-rolled arc renderers), and a webview-only fork would reintroduce it one layer up.

**3. ADR 0032 §Decision 2's enforcement clause is amended.** It currently names three enforcement points for "the IR content region carries no executable script," the second being *"`WebviewRenderAdapter` strips every JSON island."* That point is removed, because the webview no longer strips them.

**The invariant itself is unchanged and remains true.** The strip never enforced it: the same sentence explicitly *permits* inert `<script type="application/json">` data islands as "DOM data rather than code." The strip existed for **dead weight** — Story 20.9's finding that ~4.5 MB of islands rode into a surface that could never read them — and that rationale expires the moment the shell ships an engine that reads them. Enforcement now rests on the two points that genuinely carry it: the boot marker is emitted outside the captured region, and `IrSurface.vue` throws at build time on a region carrying an executable island.

**4. Two host exceptions are retired (`data-island`, `hierarchy-chart`) and one is narrowed (`asset.js`).** The two retired entries registered divergences that no longer exist: regions ship their islands, and charts mount.

`asset.js` stays registered, with its reason rewritten. It previously read "the specscribe.js enhancement script is deliberately absent" — a statement about missing *behaviour*. What survives is only a difference of **carrier**: the parity fact is a `<script src="…" defer>` tag, and this surface has none because `localResourceRoots` is empty and nothing may load from disk. Same bytes of JavaScript, delivered inline. That is precisely the shape `asset.css` has always had.

The webview's registered set is therefore `asset.css`, `asset.js`, `mermaid` — two carrier differences and one CSP casualty, none of them a missing capability.

*(Recorded because the first draft of this ADR got it wrong: it declared `asset.js` fully retired on the assumption that shipping the whole script erased the fact. The parity harness compares the `<script src>` **tag**, so the divergence was still real, and the chrome-parity tests failed. The harness was right and the ADR was wrong.)*

**5. This covers hierarchy charts and Epic 24 relationship graphs together**, satisfying ADR 0030's requirement that the two be "decided once, for both." Both read inert islands through the same shared engine (ADR 0030 measured `scatter` as already registered in the bundle, so the marginal cost is zero bytes), so a decision that lit up one and not the other would be arbitrary.

**6. The payload cost is accepted, explicitly, by owner decision (2026-08-01).** Preserving every island adds **4,528,007 B** measured across 167 pages; the engine adds **1,223,563 B** once per document. A 128 KB per-island budget was offered — the measured distribution has a natural 15× cliff between `story-23-2.html` (81,884 B) and `code-map.html` (1,243,124 B) — and the owner chose **no budget**. Recorded here because a payload decision made silently is one nobody can revisit.

## Consequences

**Good.**

- The primary defect is fixed: charts render in-editor, on the surface a reader most often opens.
- No security posture moves. The strictest clause in the project stays exactly as strict, and this ADR adds a second record saying so.
- ADR 0013 §Decision 7's webview fallback stops being the *primary* presentation and becomes what it was always meant to be — a fallback for when the mount fails.
- ADR 0030's "decided once, for both" is satisfied without a second ADR.

**Bad, and accepted.**

- **The webview payload grows by ~5.75 MB** (islands + engine). The extension holds this in memory and `JSON.parse`s it before painting, so it is not free — it lands in the same change as work reducing that payload, and the net effect must be measured rather than assumed.
- **`code-map.html`'s 1.24 MB island returns**, the exact weight Story 20.9 removed. It is no longer dead — the engine reads it — but it is the single largest line item and the first thing to revisit if payload size bites.
- **Mermaid remains the one CSP casualty**, unchanged since ADR 0005 and restated by ADR 0032. Nothing here addresses it.
- **This narrows the gap between "the webview ships no JS" and reality.** ADR 0005's original framing of a script-free body is now further from the artefact than ADR 0032 already found it to be. The invariant that matters — no *executable* script *inside the region* — is unchanged, but a reader skimming ADR 0005 alone will be misled, which is why this record exists.

**Neutral.**

- No text-twin audit is owed. ADR 0031 retired ADR 0013's per-story gate and moved standardization to Epic 28; any twin gap on these surfaces is tracked debt owed there, not a blocker here.

## Alternatives considered

**Write a new ADR deciding the webview-chart CSP question.** Rejected — this was the seeded plan and it was wrong. ADR 0032 already discharged ADR 0012 §Decision 5, and that clause exists precisely to stop two records of one posture from drifting apart.

**Leave the strip and render the text twin visibly instead** (ADR 0013 §Decision 7's accepted degradation). Rejected: it is a fallback for a technical limit that does not exist. The `hierarchy-chart` exception itself called the absence a sequencing choice.

**Keep a per-island byte budget.** Offered to the owner with measured data and declined; recorded in Decision 6 rather than dropped.

**Relax the CSP to load the engine from disk via `localResourceRoots`.** Rejected: strictly worse. It loosens the policy and adds a resource-root surface to obtain something inline nonce'd script already delivers.

**Amend ADR 0032 in place rather than writing this record.** Rejected: ADR 0032 is still `Proposed` and unratified. Editing an unratified decision to accommodate later work would obscure what the owner is being asked to ratify.

## Open items

- **ADR 0032 is `Proposed`, not Accepted.** This ADR leans on its reasoning and amends one of its clauses; the two want ratifying together.
- ADR 0032's own open question is untouched and still live: *does the webview eventually consume the Nuxt output, or stay on the region path permanently?* If it moves, Story 23.1's `'strict-dynamic'` finding becomes live again and both records must be revisited.
