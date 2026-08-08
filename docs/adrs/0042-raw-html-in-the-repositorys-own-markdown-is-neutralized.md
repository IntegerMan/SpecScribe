# ADR 0042: Raw HTML in the Repository's Own Markdown Is Neutralized, Not Carried

**Status:** Proposed (authored 2026-08-08 by Story 17.2; ratification is the owner's)
**Date:** 2026-08-08
**Deciders:** Matthew-Hope Eland
**Amends:** [ADR 0021](0021-carrying-foreign-artifacts-verbatim-into-the-portal.md) — extends its no-script policy to a second class of input, and records why that class gets a *different* remedy
**Relates to:** [ADR 0016](0016-ir-carries-rendered-prose-html.md) (the verbatim carriage this preserves for benign content); [ADR 0032](0032-csp-posture-after-the-projection-layer.md) (the webview policy that already blocked this vector, and the script-island invariant that did not); [ADR 0013](0013-text-twin-is-the-no-js-contract.md) (no hole is closed by making a surface JS-dependent); [ADR 0043](0043-the-generated-static-site-carries-no-csp.md) (the defense-in-depth half, decided separately)

## Context

SpecScribe renders markdown from **arbitrary third-party repositories** into a static HTML portal. Story 17.2 measured what that means end to end.

`MarkdownConverter.BuildPipeline()` builds `new MarkdownPipelineBuilder().UseAdvancedExtensions()` with **no `DisableHtml()`**, so raw HTML in a source `.md` passes through. ADR 0016 makes that rendered prose HTML the IR's payload, carried **verbatim** as strings. `web/components/surfaces/IrSurface.vue` injects it with `v-html`. The generated static site carries **no CSP** (ADR 0043).

The codebase's stated defense, at `IrSurface.vue:34`, is that "`v-html` never executes injected `<script>` tags." **That is true and it is not sufficient.** `innerHTML` does not run `<script>`, but it does run `<img src=x onerror=…>`, `<svg onload=…>`, and `javascript:` URLs. ADR 0032's measured invariant (0 executable scripts in-region, 163 inert JSON islands) and `IrSurface.vue`'s build-time throw both key on **script islands only** — neither looks at event-handler attributes.

### The asymmetry that made this a defect rather than a judgement call

ADR 0021 §Decision already writes the exact policy, for *foreign* carried HTML:

> **No script.** Not a `<script>` tag, not an inline event handler, not a `javascript:` URL, and not an embedding element (`<iframe>`/`<object>`/`<embed>` — an `iframe srcdoc` executes).

…and `IdeaDiscovery` already implements it. So a foreign `forge-report.html` was refused unless script-free — while the repository's **own** `.md` files, reaching the same output through the same `v-html`, were not checked at all. Same threat, opposite treatment, policy language already written and already shipped.

## What was measured (2026-08-08, baseline `e8a689d`)

Not reasoned — reproduced. A `.md` fixture was generated through the real pipeline and the shipped `.html` was read directly. **Every vector survived verbatim:**

```html
<img src=x onerror="window.__SPECSCRIBE_XSS_IMG=1">
<svg onload="window.__SPECSCRIBE_XSS_SVG=1"></svg>
<a href="javascript:window.__SPECSCRIBE_XSS_HREF=1">click</a>
<a href="javascript:window.__SPECSCRIBE_XSS_MD=1">md link</a>
<iframe srcdoc="&lt;svg onload=…&gt;"></iframe>
<object data="data:text/html,…"></object>
```

Three findings the story did not predict:

1. **The cheapest vector needs no raw HTML at all.** Ordinary markdown link syntax `[text](javascript:alert(1))` parses to a `LinkInline`, and Markdig writes its `Url` straight into an `href`. A fix that only inspected raw-HTML passthrough would have missed it entirely.
2. **A literal `<script>` was already a denial-of-service, not an injection.** It reached the IR, tripped `IrSurface`'s executable-island throw, and the page returned HTTP 500 (`errors=1`). So hostile markdown could *delete a page* from the portal. Escaping it closes that too.
3. **`<base>` and `<meta http-equiv=refresh>` carry neither a handler nor a `javascript:` URL**, so handler-stripping alone would not have caught them — and `<base>` silently re-points every relative URL on the page.

The **webview is unaffected**: `script-src 'nonce-…'` with no `'unsafe-inline'` blocks inline handlers, and that policy string is unchanged at HEAD (`WebviewRenderAdapter.cs:63-64`). The **static site was affected**, and this repository publishes to GitHub Pages — so the realistic impact was stored XSS on a `*.github.io` origin.

## Decision

**1. Raw HTML from the repository's own markdown is NEUTRALIZED at the Markdig render seam.** Executable constructs are removed; everything else passes through byte-identically.

**2. The remedy differs from ADR 0021's, and the distinction is argued rather than assumed.** ADR 0021 explicitly rejects sanitising-by-transformation for carried artifacts, because "that produces a document the author did not write while still presenting it as the original." That reasoning is scoped to artifacts carried **verbatim as whole documents**. The repository's own markdown is *already* transformed by Markdig on its way to HTML — transformation is the entire contract of that path — so a further transformation at the same seam does not misrepresent an original the way rewriting a carried `forge-report.html` would. **Detection-and-refuse stays right for carried artifacts; neutralize-and-render is right here**, because refusing would mean deleting a page of the user's own documentation over a `<br>`.

**3. The policy has ONE home.** `HtmlSafety` owns the definition of "executable"; `IdeaDiscovery` keeps owning the *decision* to reject. The regex formerly duplicated in `IdeaDiscovery` moved there. A second copy is precisely the SSOT defect Story 17.1 is sweeping up.

**4. Neutralization is defined as:**

| construct | treatment | why not something else |
| --- | --- | --- |
| `on*` attributes | dropped, element kept | the element itself is usually the author's content |
| `javascript:` / `vbscript:` / `data:text/html` / `data:image/svg+xml` URLs | attribute dropped; markdown-syntax links blanked to `href=""` | link **text** is authored prose and stays visible |
| `<script>`, `<iframe>`, `<object>`, `<embed>`, `<base>`, `<form>`, `<meta>`, `<link>` | escaped to visible text | an `iframe srcdoc` executes; `<base>` hijacks every relative URL. Escaping rather than deleting lets a reader see what the source claimed |
| `srcdoc`, `http-equiv` | dropped | an inline document, so scheme-checking is meaningless |
| `style` containing `url(` or `expression(` | dropped | the portal's own markdown authors no inline styles, so parsing CSS is not worth it |
| anything unparseable | escaped whole | **fails closed** — being unable to prove a fragment inert is treated as proving it dangerous |

**5. The sanitizer operates on RAW HTML PASSTHROUGH NODES ONLY — never on rendered output.** This is load-bearing, not an implementation note. **This portal renders its own source.** The string `onerror=` appears legitimately — escaped — inside code spans and fences on the generated Code Map, in `IdeaDiscovery.cs`'s own rendered source, and on this very ADR's page. A regex pass over finished HTML would rewrite that documentation while every gate stayed green. Markdig's `HtmlBlock`/`HtmlInline` nodes and link destinations exclude code spans and fences **by construction**, because those are separate node types Markdig already escapes.

## Consequences

**ADR 0016's verbatim carriage is preserved where it matters.** Benign structural HTML this repository already uses — `<details>`, `<summary>`, `<kbd>`, `<br>`, `<span>`, `<sub>`/`<sup>`, `<abbr>` — passes through byte-identically, pinned by test. Epic 23's central finding (no Vue reimplementation of ~889 LOC of custom renderers) is not forfeited: the IR still renders verbatim, it just never contains the handler.

**Blanket-escaping raw HTML was considered and rejected on measured cost.** This repository's own `epics.md` uses `<details>` today. Escaping all raw HTML blocks would have visibly broken shipped documentation to close a hole that targeted neutralization closes without cost.

**A hostile repository can no longer break a page, either.** The `<script>` denial-of-service closes as a side effect.

**The IR is clean at source, so both surfaces benefit.** Sanitizing in the renderer would have left the IR itself carrying live handlers for any other consumer, and would have put the check on the far side of ADR 0016's verbatim boundary.

**This is not a substitute for a CSP.** It is the primary control; ADR 0043 decides the defense-in-depth layer separately, and deliberately does not bundle the two.

**Cost:** 33 regression tests (`HtmlSafetyTests`), one new ~250-line file, and a per-tag rewrite on raw-HTML nodes only — no measurable generation-time change at this repository's scale (510 pages, unchanged wall clock).
