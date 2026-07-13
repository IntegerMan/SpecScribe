# Prism vendoring

The in-portal code pages (Story 7.1) syntax-highlight source with a **vendored** Prism.js bundle — no CDN, so the
generated site works offline and on GitHub Pages, and the global-tool package stays self-contained.

The shipped artifacts are committed as embedded resources:

- [`src/SpecScribe/assets/prism.js`](../../src/SpecScribe/assets/prism.js) — Prism core + languages (dependency
  ordered) + the SpecScribe line-aware driver ([`driver.js`](./driver.js)).
- [`src/SpecScribe/assets/prism.css`](../../src/SpecScribe/assets/prism.css) — the "tomorrow" token palette;
  SpecScribe neutralizes its block background and tunes a couple of token colors in `specscribe.css`
  (`.code-file .token.*`).

They're copied to the output root **only when in-portal code pages are generated**, so sites without code pages
stay byte-identical.

## Regenerate

Run only when refreshing the bundle (new Prism version or added languages):

```sh
cd tools/prism-vendor
npm install
node build.js
```

Then rebuild the .NET project so the embedded resources pick up the new files, and re-run the golden fingerprint
test (a CSS/asset change is expected to move it — re-baseline deliberately).

## Why a custom driver instead of a Prism plugin

Code pages render each line server-side as `<span class="code-line" id="L{n}" data-ln="{n}">…</span>` so the locked
`#L{n}` deep-link anchors and the CSS gutter work **with JavaScript disabled**. A drop-in highlighter (or Prism's
`keep-markup` plugin) rewrites the `<code>` element and mangles or duplicates those per-line wrappers. The driver
instead keeps Prism in manual mode, tokenizes the whole block once (correct multi-line constructs), splits the
highlighted HTML by line, and injects each fragment into the existing `.code-line` span — anchors untouched.

Language selection lives in `build.js` (`WANT`); the file-extension → grammar mapping lives in
`CodeFileTemplater.LanguageClass`. Keep the two in sync.
