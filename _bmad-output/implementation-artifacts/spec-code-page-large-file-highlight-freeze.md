---
title: 'Fix browser freeze on large code pages (stale vendored Prism bundle)'
type: 'bugfix'
created: '2026-07-13'
status: 'done'
route: 'one-shot'
---

# Fix browser freeze on large code pages (stale vendored Prism bundle)

## Intent

**Problem:** Code pages for large source files (e.g. `code/src/SpecScribe/assets/specscribe.css.html`, 3986 lines) load then hang the tab. Root cause: `src/SpecScribe/assets/prism.js` had regressed to an older auto-highlight bundle whose KeepMarkup plugin does an O(n²) DOM re-walk per rendered line to preserve the `#L{n}` anchors — an unrelated prior commit (`1a2b7f5`, "Scribe's Nib branding and VS contrast pass") clobbered the correct bundle that Story 7.1 had already shipped.

**Approach:** Regenerate `prism.js`/`prism.css` from the existing, already-committed vendoring tool (`node tools/prism-vendor/build.js`), which reassembles Prism in manual mode plus the line-aware `driver.js` — it tokenizes each block once and splits the result by line instead of relying on KeepMarkup's per-node DOM range search. No source-code redesign; this restores previously-shipped, previously-tested behavior.

## Suggested Review Order

- The regenerated vendored bundle — verify it was produced by the build script, not hand-edited.
  [`prism.js`](../../src/SpecScribe/assets/prism.js)

- The build recipe that produced it (unchanged, but is the source of truth for the regenerated bundle).
  [`build.js`](../../tools/prism-vendor/build.js)

- The line-aware driver the bundle now runs (unchanged; re-included by the regeneration).
  [`driver.js`](../../tools/prism-vendor/driver.js)

- Comment correction: the Swift grammar is present in the regenerated bundle, so the old "not vendored" rationale was stale. `.swift` still isn't wired to a language class (separate decision) — only the comment changed.
  [`CodeFileTemplater.cs:636`](../../src/SpecScribe/CodeFileTemplater.cs#L636)

**Verification performed:** built a standalone repro harness reproducing `specscribe.css`'s exact 3986-line `.code-line` markup against both bundles. Old bundle: ~139.5s of blocked highlighting. Regenerated bundle: ~0.8s. `dotnet build` of the main `SpecScribe` project succeeds (the test project has unrelated, pre-existing build errors from a concurrent session's in-progress `CodeMap` work, not touched here).
