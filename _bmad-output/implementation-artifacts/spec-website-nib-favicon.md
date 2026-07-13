---
title: 'Website nib favicon'
type: 'chore'
created: '2026-07-13'
status: 'done'
review_loop_iteration: 0
context: []
baseline_commit: '1a2b7f5b4a0f59bb6d07e5af48819f65a2459e85'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The generated website's browser-tab favicon is still the old "gold quill-spark" star (`PathUtil.FaviconSvg`, Story 1.5) — off-brand now that the VS Code extension and the site nav both carry the Scribe's Nib mark (`1a2b7f5`). The one at-a-glance surface that doesn't match the VS Code nib icon is the tab icon on every page.

**Approach:** Replace the star favicon with a nib rendition that mirrors the VS Code panel icon (`extension/media/specscribe.svg`): a teal rounded tile, a parchment nib, and a gold vent. Build it from the *existing* `HtmlRenderAdapter.NibPathData` geometry constant (single source) rather than a new hand-drawn path, so no fourth divergent rendition is introduced.

## Boundaries & Constraints

**Always:**
- Favicon geometry MUST come from `HtmlRenderAdapter.NibPathData` (reuse, not re-copy) — the nib silhouette with its `evenodd` vent + slit cutouts.
- Match the VS Code panel icon's self-contained palette exactly: tile `#2e6b7a`, nib `#f5f0e8`, vent `#d4a017`. Hardcoded hex is correct here — a data-URI favicon is an isolated asset outside the CSS token system (same as the existing favicon and `specscribe.svg`); the "no hex in markup" token rule governs CSS-driven chrome, not standalone icon assets.
- Stay a self-contained inline-SVG data URI emitted from `RenderHeadOpen` — no new asset file, no new dependency.
- Keep the existing percent-encoding of the whole SVG (`Uri.EscapeDataString`) so the data URI stays RFC-3986-valid.

**Ask First:**
- Any move of `NibPathData` to a new home, or any change to the nib silhouette/palette itself.
- Introducing an `og:image`/social-preview card (explicitly deferred — see `deferred-work.md`).

**Never:**
- Do not add an image dependency (SkiaSharp/ImageSharp/System.Drawing) or a raster asset.
- Do not touch the site nav mark, the two `extension/media` SVGs, or the CSS token system.
- Do not add `og:image`/`og:url`/`--site-url` in this spec.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Any generated page | `RenderHeadOpen` builds `<head>` | `<link rel="icon">` href is a data-URI SVG = teal tile + `NibPathData` nib + gold vent; NOT the old star path | N/A |
| Single-source check | Favicon SVG string | Contains the exact `NibPathData` `d` value verbatim | N/A |
| SVG-favicon-incapable browser | Legacy UA ignores SVG icons | No tab icon (same profile as the prior SVG-only favicon — no regression) | Degrade to no icon |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/PathUtil.cs` -- `FaviconSvg` const (lines ~43–47) + its doc comment; `FaviconDataUri` (~51) and `RenderHeadOpen` (~79) consume it unchanged. THE change site.
- `src/SpecScribe/HtmlRenderAdapter.cs` -- `public const string NibPathData` (~58–62), the shared 24-box nib geometry the favicon will reference. Read-only here.
- `extension/media/specscribe.svg` -- the VS Code panel icon this favicon mirrors (teal tile / parchment nib / gold vent). Visual reference only.
- `tests/SpecScribe.Tests/PathUtilTests.cs` -- `RenderHeadOpen_EncodesTitleAndStylesheetHref` asserts favicon *presence* (stays green); add the new nib-favicon test here.

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/PathUtil.cs` -- Replace `FaviconSvg` with a 24-box (`viewBox='0 0 24 24'`) SVG: a rounded teal tile (`#2e6b7a`), then `<path fill-rule='evenodd' fill='#f5f0e8' d='{HtmlRenderAdapter.NibPathData}'/>`, then a gold vent `<circle cx='12' cy='9.2' r='2.1' fill='#d4a017'/>` filling the evenodd vent cutout. Rewrite the doc comment: describe the nib rendition, cross-reference `NibPathData` + `specscribe.svg`, drop the stale "gold quill-spark / Story 1.5" wording. Leave `FaviconDataUri`/`RenderHeadOpen` untouched.
- [x] `tests/SpecScribe.Tests/PathUtilTests.cs` -- Add a test that renders a head, extracts the `rel="icon"` href, `Uri.UnescapeDataString`s it, and asserts the decoded SVG (a) contains `HtmlRenderAdapter.NibPathData` verbatim, (b) contains `#2e6b7a` and `#d4a017`, and (c) does NOT contain the old star marker (e.g. `18.4 13.6`). Covers the I/O matrix rows.

**Acceptance Criteria:**
- Given any generated page, when its `<head>` is rendered, then the `rel="icon"` data-URI SVG decodes to a teal-tiled nib with a gold vent and contains no trace of the previous gold-star path.
- Given the favicon SVG, when compared to `HtmlRenderAdapter.NibPathData`, then it embeds that exact path string (proving one shared geometry source, not a fourth copy).
- Given the full test suite, when `dotnet test` runs, then it is green — the pre-existing favicon-presence assertions (`PathUtilTests`, `HtmlTemplaterTests`) still pass unchanged.

## Spec Change Log

- **Golden fingerprint regeneration (impl-time discovery).** The favicon is emitted in every page's `<head>` via `RenderHeadOpen`, so `SiteGeneratorAdapterTests.GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens` moved (its normalizers cover the footer clock / `?v=` / version / build rows — not the favicon, correctly). Regenerated the pinned constant `96ae1efd…` → `4c2ce594b3c8db103fab66e0484df193298c8d5567c9a776f4154c5af8533dd8`, per the test's own "regenerate ONLY as a deliberate, reviewed decision" guard. The test wasn't in the Code Map — planning content-searches skipped that file (a tool reads it as binary) — but no spec intent changed.

## Design Notes

Composition faithfully reproduces `specscribe.svg` while reusing one geometry source: the nib path already cuts the vent as an `evenodd` hole, so filling that hole with a gold `<circle>` (center `12,9.2`, r `2.1` — the diametric midpoint of `NibPathData`'s vent arc) yields the same parchment-nib-with-gold-vent look, and the slit cutout lets the teal tile show through exactly as the panel icon does. Nudge `r` to `2.2` only if an anti-alias seam shows.

`PathUtil` referencing `HtmlRenderAdapter.NibPathData` is a compile-time `const` (inlined into `PathUtil`'s IL — no runtime type-init dependency, no cycle, no layering behavior coupling); a source-level reference within the one `SpecScribe` assembly. Relocating the const to a neutral `Brand` home is the tidier long-term move but is out of scope (Ask First) — it would churn `HtmlRenderAdapter` and its nav tests for no visual gain.

## Verification

**Commands:**
- `dotnet test` -- expected: all green, including the new nib-favicon test.

**Manual checks:**
- Build and generate a site (`dotnet run --project src/SpecScribe -- generate` against this repo → `SpecScribeOutput`), open any page, and confirm the browser tab shows the teal nib tile (matching the VS Code panel icon), not the gold star.

## Suggested Review Order

- Entry point — the favicon itself: teal tile + reused `NibPathData` nib + gold vent, replacing the star.
  [`PathUtil.cs:50`](../../src/SpecScribe/PathUtil.cs#L50)
- The single-source decision + hardcoded-hex rationale live in the rewritten doc comment above the const.
  [`PathUtil.cs:40`](../../src/SpecScribe/PathUtil.cs#L40)
- Behavior pin: decodes the emitted favicon and asserts nib geometry + palette, guards the star regression.
  [`PathUtilTests.cs:73`](../../tests/SpecScribe.Tests/PathUtilTests.cs#L73)
- Golden fingerprint regenerated (every-page `<head>` shifted); comment records why.
  [`SiteGeneratorAdapterTests.cs:242`](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs#L242)
