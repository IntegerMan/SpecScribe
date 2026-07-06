---
title: 'Colorize color values in inline code spans'
type: 'feature'
created: '2026-07-06'
status: 'in-progress'
review_loop_iteration: 0
context: []
baseline_commit: '08c210ea88086a02c18d24edcae37f57bdf80a42'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** When docs mention color values (hex codes, rgb/rgba functions, CSS named colors) inside inline code spans, they render as plain text — the reader can't see the actual color, and color-token tables (like the palette table) are hard to scan.

**Approach:** Add an HTML post-processor that detects inline `<code>` spans whose entire content is a single recognized color value and paints the swatch by setting the span's background to that color, with an automatically chosen black/white foreground that maximizes readable contrast (WCAG relative luminance). Recognizes CSS hex (`#rgb`/`#rgba`/`#rrggbb`/`#rrggbbaa`), `rgb()`/`rgba()` functions, and the standard CSS named colors. Runs at the markdown→HTML boundary so every doc surface benefits.

## Boundaries & Constraints

**Always:** Colorize only when the code span's *entire* trimmed text is one recognized color value. Foreground is pure black or white, whichever yields the higher WCAG contrast ratio against the effective (alpha-composited over white) swatch color. Preserve the original value text verbatim. Add a shared `color-swatch` CSS class plus per-span inline `style`. Be idempotent and safe on already-rendered HTML.

**Ask First:** Supporting color syntaxes beyond hex, `rgb()`/`rgba()` (comma syntax), and CSS named colors — e.g. `hsl()`, modern space/slash `rgb(255 0 0 / 50%)`, or project palette tokens like `cream`/`rust`.

**Never:** Colorize block/fenced code (`<pre><code>`). Colorize spans containing extra text (e.g. `background: #fff`). Introduce a client-side script — this is server-side rendering only. Depend on a dark theme (none exists).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Dark hex | `<code>#1a1208</code>` | `background:#1a1208`, white foreground, `color-swatch` class | N/A |
| Near-white hex | `<code>#f5f0e8</code>` | swatch background set, black foreground | N/A |
| rgba with alpha | `<code>rgba(26, 18, 8, 0.94)</code>` | background = the rgba value, white foreground (composited over white for luminance) | N/A |
| CSS named color | `<code>teal</code>` | background `teal`, white foreground | N/A |
| Non-CSS name | `<code>cream</code>` | left untouched (not a CSS named color) | Leave as-is |
| Invalid hex | `<code>#zzz</code>` | left untouched | Leave as-is |
| Mixed content | `<code>background: #fff</code>` | left untouched | Leave as-is |
| Block code | `<pre><code>#fff</code></pre>` | left untouched | Leave as-is |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/ColorSwatchRewriter.cs` -- NEW. Static `Rewrite(string html)` post-processor; color parsing + contrast logic + CSS named-color map.
- `src/SpecScribe/MarkdownConverter.cs` -- Apply `ColorSwatchRewriter.Rewrite` to `BodyHtml` (in `Convert`), and to `RenderBlock`/`RenderInline` output so fragments (AC lines, epic overviews) match.
- `src/SpecScribe/assets/specscribe.css` -- Add `.doc-body code.color-swatch` rule: thin border for visibility of near-page-color swatches, normal weight, drop the inherited rust color (inline style wins for the specific colors).
- `tests/SpecScribe.Tests/ColorSwatchRewriterTests.cs` -- NEW. Cover the I/O matrix rows.

## Tasks & Acceptance

**Execution:**
- [ ] `src/SpecScribe/ColorSwatchRewriter.cs` -- Add static rewriter: regex-match inline `<code>value</code>` (no attributes, not inside `<pre>`), parse hex/rgb()/rgba()/CSS-named to RGBA, composite over white, compute WCAG relative luminance, pick black/white foreground, emit `<code class="color-swatch" style="background:VALUE;color:FG">value</code>`. Unrecognized values pass through unchanged.
- [ ] `src/SpecScribe/MarkdownConverter.cs` -- Wrap the three markdown→HTML outputs (`Convert` BodyHtml, `RenderBlock`, `RenderInline`) with `ColorSwatchRewriter.Rewrite`.
- [ ] `src/SpecScribe/assets/specscribe.css` -- Add `.doc-body code.color-swatch` styling (1px border using `--border`, `color` unset so inline style governs, keep radius/padding/monospace).
- [ ] `tests/SpecScribe.Tests/ColorSwatchRewriterTests.cs` -- Unit-test each I/O matrix row incl. the contrast foreground choice and pass-through cases.

**Acceptance Criteria:**
- Given a doc with `` `#1a1208` `` and `` `#f5f0e8` `` in a table, when the site is generated, then each cell's code span shows its background as the actual color with a readable (max-contrast black/white) foreground.
- Given a code span whose text is not a lone recognized color value, when rendered, then the span is emitted unchanged (no `color-swatch` class, no inline style).
- Given fenced/block code containing a hex value, when rendered, then it is not colorized.

## Design Notes

Contrast: relative luminance L per WCAG (linearize sRGB channels, `0.2126R+0.7152G+0.0722B`). Contrast ratio `(Ll+0.05)/(Ls+0.05)`. Compute ratio for black and white foreground against the swatch's *effective* color (alpha `a` composited over white: `c' = c*a + 255*(1-a)`), pick the higher. Background inline style keeps the *original* value string so transparency renders naturally over the page.

Matching: `(?<!<pre>)<code>([^<]*)</code>` isolates inline code (Markdig emits attribute-less `<code>` for inline, `<code class="language-…">`/`<pre><code>` for blocks; the lookbehind excludes indented `<pre><code>`). Content has no nested tags so `[^<]*` is safe. Trim before matching a color; require the whole trimmed content to be one value.

## Verification

**Commands:**
- `dotnet build SpecScribe.slnx` -- expected: builds clean.
- `dotnet test SpecScribe.slnx` -- expected: new `ColorSwatchRewriterTests` pass, no regressions.
- `dotnet run --project src/SpecScribe -- generate --source _bmad-output --adrs docs/adrs --output docs/live --project-name SpecScribe` -- expected: regenerates site; color-token tables render with painted swatches.
