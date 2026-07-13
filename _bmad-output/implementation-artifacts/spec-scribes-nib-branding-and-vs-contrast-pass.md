---
title: "Scribe's Nib Branding and VS Contrast Pass"
type: 'feature'
created: '2026-07-12'
status: 'done'
review_loop_iteration: 0
baseline_commit: '2f30ef9b696157c96d6c931304264f7bc138313d'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** SpecScribe has no real brand mark: the activity bar shows a generic bulleted-list glyph, and the app header (site/webview/SPA) is plain text. Separately, the owner's F5 review flagged contrast: the refinement funnel's stage-linking connector is indistinguishable in the VS experience, and light text on light backgrounds reads poorly elsewhere (the known `--ink-light` ~3.9:1 debt).

**Approach (owner-selected direction: "Scribe's Nib"):** one fountain-pen-nib mark, reused everywhere — a distinctive nib silhouette for the alpha-masked activity-bar icon, a refreshed teal-tile + nib panel-tab icon, and a small two-tone inline-SVG nib beside the header wordmark via the ONE `RenderNavMarkup` seam (all three surfaces inherit it). Contrast fixed WITHIN the warm manuscript identity: a new `--funnel-connector` token (visible on the site, re-valued per webview theme in the bridge), and `--ink-light` deepened to clear WCAG AA on both parchment surfaces. Status accents are NOT re-tuned (the owner's complaint was the connector, not the stage colors), so the six `specscribe.status.*` manifest colors stay untouched per the 6.9 co-tuning constraint.

## Boundaries & Constraints

**Always:**
- One mark geometry, three renditions (activity-bar silhouette, panel tile, in-app inline SVG) — no per-surface freelancing on the shape.
- The in-app mark rides `RenderNavMarkup`'s brand span only — one seam, three surfaces; colors routed through existing CSS tokens/currentColor (token-system rule), never hardcoded hex in markup.
- Contrast fixes are TOKEN re-values (`--ink-light`, new `--funnel-connector`) + theme-bridge re-values — never per-element hex forks.
- New/changed token pairs must be pinned by a computed WCAG relative-luminance test (≥4.5:1 for normal text; the connector needs distinguishability vs the panel background in each theme block, asserted as distinct-value presence).
- This is an INTENTIONAL render change: regenerate the golden fingerprint constant once, after all visual changes land (golden-diff memory process).
- Alpha-mask reality: the activity-bar SVG conveys shape only — the nib must read at 24px as a filled silhouette.

**Ask First:** re-tuning any of the six status accents (would drag the manifest colors with it per the 6.9 constraint); touching nav LINK structure (Story 10.1 owns nav re-architecture — the brand span itself is fair game); adding a site favicon.

**Never:** change `BmadCommands`/outline/payload contracts; touch the Marketplace icon or packaging (Story 16.5); introduce JS for any of this (charts stay pure SVG+CSS).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Activity bar, any theme | dark/light/HC | Nib silhouette tinted by host foreground, legible at 24px | N/A |
| App header, all 3 surfaces | site/webview/SPA | Nib mark + wordmark aligned in the brand span, one seam | N/A |
| Funnel on parchment site | light site theme | Connector visibly links stages (distinct from background) | N/A |
| Funnel in webview dark/HC | `.vscode-dark`/HC blocks | Connector re-valued per theme, clearly visible | N/A |
| Muted text on parchment-dark | `--ink-light` on `--parchment-dark` | ≥4.5:1 (currently ~3.9:1) | N/A |
| Muted text on parchment | `--ink-light` on `--parchment` | stays ≥4.5:1 after deepening | N/A |
| Reduced motion / no JS | any | Mark is static inline SVG; zero behavioral surface | N/A |
| Golden fingerprint | after regen | passes with the NEW constant; single regen commit-note | N/A |

</frozen-after-approval>

## Code Map

- `extension/media/specscribe-outline.svg` -- generic list glyph → nib silhouette (alpha-masked; also used by both view icons in the manifest, so shortcuts/outline views update for free)
- `extension/media/specscribe.svg` -- panel-tab tile: keep teal tile + parchment/gold language, motif becomes the nib
- `src/SpecScribe/HtmlRenderAdapter.cs:64` -- `site-nav-brand` span: prepend the inline two-tone nib SVG (class `site-nav-mark`)
- `src/SpecScribe/assets/specscribe.css:163` -- `.site-nav-brand` sizing/gap + new `.site-nav-mark` rules; `:2005` `.funnel-connector` → `var(--funnel-connector)` token (defined near the status tokens); `--ink-light` deepened at its definition
- `src/SpecScribe/assets/specscribe-webview-theme.css` -- re-value `--funnel-connector` (and `--ink-light` mapping if the bridge remaps it) in the `.vscode-dark` / HC blocks
- `tests/SpecScribe.Tests/StylesheetTests.cs` -- token presence + a computed WCAG contrast test for the tuned pairs
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs:214` -- golden constant regen (last step)
- NOTE: concurrent session is mid-Story-7.4 in `Icons.cs`/`SiteNav.cs`/`GitMetrics.cs` — this spec must not touch those files

## Tasks & Acceptance

**Execution:**
- [x] `extension/media/specscribe-outline.svg` -- replace with a filled nib silhouette (24×24, alpha-mask-safe) -- the "old iconography" fix
- [x] `extension/media/specscribe.svg` -- refresh tile motif to the same nib (16×16, colored) -- one mark everywhere
- [x] `src/SpecScribe/HtmlRenderAdapter.cs` -- inline nib SVG (aria-hidden, currentColor + one token accent) before the wordmark in the brand span -- in-app branding via the one seam
- [x] `src/SpecScribe/assets/specscribe.css` -- `.site-nav-mark` styles; define `--funnel-connector` and route `.funnel-connector` through it (site value: visible warm tone, drop the 0.8 opacity in favor of a distinguishable fill); deepen `--ink-light` to AA on both parchment surfaces -- the contrast pass, token-routed
- [x] `src/SpecScribe/assets/specscribe-webview-theme.css` -- `--funnel-connector` per `.vscode-dark`/HC block (visible against host surfaces); verify/adjust the bridge's muted-ink mapping -- the VS-side fix
- [x] `tests/SpecScribe.Tests/StylesheetTests.cs` -- pin: `--funnel-connector` exists in base + dark bridge block; computed contrast of `--ink-light` vs `--parchment` and `--parchment-dark` ≥4.5; `.site-nav-mark` present; no hardcoded hex in the brand SVG markup -- guards the intent
- [x] `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` -- regenerate the golden constant LAST, once, with all visual changes in -- byte-truth restored

**Acceptance Criteria:**
- Given the activity bar in any theme, when the SpecScribe container shows, then the icon is a distinctive nib silhouette (not the list glyph).
- Given any of the three surfaces, when the header renders, then the nib mark appears beside the wordmark, colored via tokens only.
- Given the story-pipeline funnel in the webview dark theme, when rendered, then the stage-linking connector is plainly visible against the panel background (and likewise on the parchment site).
- Given the deepened `--ink-light`, when the contrast test computes it against both parchment tokens, then both ratios are ≥4.5:1.
- Given `dotnet test` after the golden regen, then the full suite is green and the six `specscribe.status.*` manifest colors are byte-unchanged.

## Spec Change Log

- **2026-07-12/13 (review patches, no loopback).** Both reviewers converged on the webview connector chain being wrong on real themes (stock border tokens are ~1.5:1 hairlines; some themes define them transparent, which var() fallbacks never catch; HC contrastBorder hue-collides with the active accent) — replaced with `color-mix(in srgb, var(--vscode-foreground) 30%, var(--vscode-editor-background))`: visible, hue-neutral, correct per theme by construction, degrading to the base `#c7a56d` (≈6:1 on dark) on ancient webviews. Dropped the `.funnel-connector` opacity (it re-blended the band below the tested floor — the raw token is what ships). Froze `--status-deferred` at the pre-pass literal `#7a6250` (it aliased `--ink-light`, so the text deepening was silently re-tuning a status accent — the exact owner constraint). Mark hardening: fallback `width`/`height` (no more 300×150 unstyled default), slit/vent widened to survive ~14px header rendering (geometry updated in all three renditions), path single-sourced in C# as `HtmlRenderAdapter.NibPathData`. Test hardening: renderer-side markup test (aria-hidden, no hex, shared path const), connector asserted against the surfaces the funnel actually sits on (`--warm-white`/`--cream`), `TokenValue` strips comments, `MediaBlock` misuse fixed. Golden regenerated once more. Deferred: a mechanical sync guard for the three nib renditions (needs 16.5's asset pipeline). Rejected: filled-vs-outline activity-bar critique (the owner-selected direction) and golden-race process noise. KEEP: the color-mix foreground derivation, the frozen deferred literal, and the computed-contrast test pattern.

## Verification

**Commands:**
- `dotnet test` (repo root) -- expected: green after the single golden regen
- `npm run typecheck` && `npm run build` (in `extension/`) -- expected: clean (manifest untouched except none; icons are asset swaps)

**Manual checks (if no CLI):**
- F5 dev host: activity bar shows the nib; panel tab shows the refreshed tile; webview header shows the mark; funnel connector visible in dark + HC; muted text legible. Site: open the generated dashboard and confirm the same on parchment.

## Suggested Review Order

**The mark — one geometry, three renditions**

- Entry point: the shared nib path const + the brand-span inline SVG (aria-hidden, currentColor, fallback size).
  [`HtmlRenderAdapter.cs:58`](../../src/SpecScribe/HtmlRenderAdapter.cs#L58)

- The activity-bar silhouette (alpha-masked; same geometry).
  [`specscribe-outline.svg`](../../extension/media/specscribe-outline.svg)

- The panel-tab tile (16-box scaled variant, teal/parchment/gold).
  [`specscribe.svg`](../../extension/media/specscribe.svg)

- Mark sizing rides the wordmark's em; color is pure inheritance.
  [`specscribe.css:185`](../../src/SpecScribe/assets/specscribe.css#L185)

**The contrast pass — token re-values only**

- `--ink-light` deepened to AA on both parchment surfaces; `--funnel-connector` is its own visible token.
  [`specscribe.css:17`](../../src/SpecScribe/assets/specscribe.css#L17)

- `--status-deferred` frozen at the pre-pass literal — the review catch that stopped the text tuning from re-tuning a status accent.
  [`specscribe.css:51`](../../src/SpecScribe/assets/specscribe.css#L51)

- The webview band derives from the host FOREGROUND via color-mix — not the ~1.5:1 (sometimes transparent) border hairlines.
  [`specscribe-webview-theme.css:54`](../../src/SpecScribe/assets/specscribe-webview-theme.css#L54)

**Peripherals — the pins**

- Computed WCAG ratios for the tuned pairs (comment-stripped token reads).
  [`StylesheetTests.cs:429`](../../tests/SpecScribe.Tests/StylesheetTests.cs#L429)

- Connector asserted against the surfaces the funnel actually sits on + the bridge derivation.
  [`StylesheetTests.cs:442`](../../tests/SpecScribe.Tests/StylesheetTests.cs#L442)

- Renderer-side markup guard: aria-hidden, shared path, no hex in the SVG.
  [`SiteNavTests.cs:201`](../../tests/SpecScribe.Tests/SiteNavTests.cs#L201)

- The deferred-accent freeze pin.
  [`StylesheetTests.cs:482`](../../tests/SpecScribe.Tests/StylesheetTests.cs#L482)
