---
baseline_commit: 261b3008545a066ae1b08174b77df5b4abd4fb73
---

# Story 23.3: Migrate Baseline Surfaces (Dashboard, Epics) to Vue/Nuxt over the IR

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer validating the migration approach on real surfaces,
I want the dashboard and the whole epics tree rendered via Vue/Nuxt from the IR, with the IR's own link graph
resolving and the Hierarchy Explorer actually booting,
so that migration risk is proven on high-traffic surfaces — as a navigable, interactive site, not just a
byte-comparison — before the remaining pages migrate in 23.4.

## Acceptance Criteria

_ACs 1–2 are the epic's stated ACs (epics.md §Story 23.3). ACs 3–8 are the concrete scope this story was
seeded with: the 23.1 spike gate's two additions (head projection — AC 5; route-mapping the in-content link
graph — AC 4) and the six owner decisions locked at create-story (see Dev Notes → Owner decisions)._

1. **Given** the existing golden output for `index.html`, `epics.html`, `epics/epic-{N}.html` and
   `epics/story-{id}.html`
   **When** the Vue/Nuxt versions render from the IR
   **Then** each surface's `<main>` region achieves **parity — byte-identical or documented-equivalent** with
   the pre-migration golden baseline, proven by a committed harness (`npm run measure:parity`) that normalizes
   the known non-content variance (footer clock, `?v=<AssetVersion>`, product version, CRLF/BOM) and emits a
   per-surface table. Every non-zero delta is **enumerated with its cause**, and each is traced to a
   pre-existing IR-capture defect rather than accepted as a migration cost (see Dev Notes → The proxy IR is
   lossy — do not chase these).

2. **Given** the accessibility and reduced-motion conventions (Stories 1.4, 1.5, 3.5; UX-DR16, UX-DR17)
   **When** the migrated surfaces render
   **Then** those conventions are preserved without regression: **exactly one** `<main id="main-content">` per
   page and exactly one skip link as the first focusable element, the `prefers-reduced-motion` reduce block
   applies to injected content as well as template-authored content, every status carries its **word** and is
   never signalled by color alone, and the focus ring is present on injected links.

3. **Given** Story 22.2 is `ready-for-dev` and the canonical IR does not exist yet
   **When** 23.3 reads the IR
   **Then** it consumes the shipped `SpaDelivery` output (`spa/manifest.json` + `spa/pages-*.json`) as a
   **proxy IR through exactly one adapter module** (`web/ir/adapter.ts`), no SpaDelivery field name appears
   anywhere else in `web/`, and the adapter's own doc comment states the neutral `IrSite`/`IrPage` shape that
   the rest of the app is allowed to know about — so 22.2's schema change touches **one file**.

4. **Given** the 23.1 spike proved rendering but **not** navigability — the IR's own hrefs (`code/…`,
   `adrs/…`, `epics.html`) did not resolve against the spike's route space, which is also why
   `crawlLinks: true` aborts the build
   **When** 23.3 builds the prerender route table **from the IR manifest** (routes are the IR's
   output-relative paths verbatim, so no href is ever rewritten)
   **Then** every in-content `<a href>` on the four migrated surface families **resolves to a prerendered
   file**, proven by a committed harness (`npm run check:links`) that walks the emitted output, resolves each
   href relative to its own page, and exits non-zero on a dangling internal target; and the surfaces are
   confirmed **navigable with JavaScript disabled** in a live browser, closing the half of the spike's AC #1
   that was left open.

5. **Given** Nuxt owns the document head and the golden's head is not reproduced by default
   **When** a migrated page renders
   **Then** a **head projection** derived from the IR emits the golden's head contract: `<title>`,
   `<meta name="description">`, `og:type`/`og:title`/`og:description`, the favicon `data:` URI, and the
   versioned stylesheet/script links — matched field-by-field against `PathUtil.RenderHeadOpen` and any
   deliberate difference recorded in the parity report.

6. **Given** the IR's `contentHtml` is markup authored against `specscribe.css`, and the Nuxt app deliberately
   imports **only** `tokens.css` (23.2 AC #1)
   **When** IR content is injected
   **Then** it is styled by a **second generated, drift-gated layer** — `web/assets/ir-content.css`, extracted
   from `specscribe.css` by an extended bridge script and guarded by `npm run check:tokens`' sibling
   `check:ir-content` — applied as a global sheet **scoped under the injecting wrapper**, never hand-authored
   and never a wholesale monolith import. The extraction records a **manifest of exactly which rules are still
   monolith-derived**, so the surface 23.4 has to retire is enumerated rather than implied.

7. **Given** ADR 0012 §Decision 2 ("one Hierarchy Explorer component is the only route to a sunburst or
   treemap") and ADR 0013 (the text twin is the no-JS contract)
   **When** a migrated surface carries a Hierarchy Explorer
   **Then** the chart **boots and is interactive** on the Nuxt page by **reusing the shipped implementation** —
   `initHierarchyExplorers` in `specscribe.js`, driven through the existing `specscribe:content-swapped` seam —
   **not** by reimplementing it in Vue (a second implementation is exactly what ADR 0012 exists to prevent);
   and when the bundle is absent or blocked, the page still shows the server-rendered fallback SVG and the text
   twin, unchanged.

8. **Given** `web/` is not wired into `specscribe generate` and packaging is Story 23.5's decision
   **When** this story completes
   **Then** **no production C# changes ship** — `src/SpecScribe/**` and `tests/**` are git-confirmed untouched,
   `SpecScribe.slnx` still holds two projects, and `GoldenContentFingerprint` **does not move** (a moved
   fingerprint means this story leaked into the C# renderer and must be reverted, not re-blessed); and the
   measured hydration payload stays at 23.2's build-time baseline (≈1.00×, not the 1.36×/1.99× shapes AC #4 of
   23.2 rejected), reported by `npm run measure:payload`.

## Tasks / Subtasks

- [ ] **Task 1 — Produce the proxy IR and stand up the adapter** (AC: #3)
  - [ ] Generate the IR input: `dotnet run --project src/SpecScribe -- generate --spa` into `SpecScribeOutput/`
        (the default — never `--output docs/live`). This yields `SpecScribeOutput/spa/manifest.json` +
        `spa/pages-*.json`. Also generate the **static** site in the same run for the parity oracle.
  - [ ] Write `web/ir/adapter.ts` — the **only** file in `web/` that knows the SpaDelivery shape. Port the
        neutral shape from `spike/nuxt-ir/ir/adapter.mjs` (`IrSite { title, entry, nav[] }`,
        `IrPage { path, title, contentHtml, breadcrumb[], parent, children[] }`) and **extend it** with the
        region split in Task 2.
  - [ ] Resolve the IR at **build time, at module scope, with no data composable** — 23.2's measured
        recommendation (variant C, 1.00×). Do **not** use `useAsyncData` (1.36×) and do **not** use
        `.server.vue`/`<NuxtIsland>` (1.99×). See CONVENTIONS.md §4.
  - [ ] Point the adapter at the IR directory through one configurable path (env var or `runtimeConfig`), so a
        dev does not have to hand-edit a literal. Do not commit the IR fixtures.

- [ ] **Task 2 — Split the content region; do not nest `<main>`** (AC: #2, #3)
  - [ ] ⚠️ `contentHtml` is **not** just the body. `SpaDelivery.ExtractContentRegion` (`SpaDelivery.cs:76–99`)
        returns `navMarkup + [breadcrumb] + <main id="main-content">…</main>`. `PageShell.vue` **already emits
        its own** `<main id="main-content">` and skip link. Injecting `contentHtml` inside it produces a
        **nested `<main>` and a duplicate `id`** — an a11y defect and a parity failure.
  - [ ] In the adapter, split the region into `{ navHtml, breadcrumbHtml, mainAttributes, mainInnerHtml }`
        using the same markers `ExtractContentRegion` used to build it (`<div class="breadcrumb"`,
        `<main id="main-content"`, the matching `</main>`). Fail loudly on a page that does not match rather
        than silently emitting half a page.
  - [ ] Feed `navHtml` to `PageShell`'s existing `#nav` slot, and `mainInnerHtml` into `<main>`. Verify the
        `<main>` open-tag attributes the C# templaters emit per surface and let `PageShell` reproduce them, so
        the `<main>` region compares byte-for-byte.

- [ ] **Task 3 — Route table from the IR manifest** (AC: #4)
  - [ ] Build `nitro.prerender.routes` from `manifest.pages` at config time. Keep `crawlLinks: false`
        (23.1 finding 8 — the crawler follows the injected IR's own links and aborts the build on the first
        404).
  - [ ] **Routes are the IR's output-relative paths verbatim, with a leading slash** (`/index.html`,
        `/epics.html`, `/epics/epic-3.html`). This is load-bearing: it means the IR's relative hrefs
        (including `../` prefixes on nested pages) resolve **unchanged**, so no href is ever rewritten and the
        injected strings stay byte-identical. Rewriting links into a clean route space was considered and
        rejected for exactly this reason.
  - [ ] Add a `/` route resolving to the manifest `entry` so the site root works.
  - [ ] ⚠️ **All routing goes through one `pages/[...path].vue` catch-all.** Because routes carry a `.html`
        extension, Nuxt's file-based routing cannot express them (there is no valid `pages/epics.html.vue`).
        The catch-all resolves `route.params.path` against the manifest and **branches to a surface component**
        by path; every **non-migrated** page falls through to a minimal pass-through (`PageShell` + injected
        region, no surface-specific components). Pass-throughs exist so the link graph resolves end-to-end;
        label them in code as **23.4's to upgrade**, not a migration claim.

- [ ] **Task 4 — Componentize the four migrated surface families** (AC: #1, #2)
  - [ ] `index.html` (dashboard), `epics.html`, `epics/epic-{N}.html`, `epics/story-{id}.html`
        (`StoryEpicLinkifier.StoryPagePath` — `epics/story-{id-with-dashes}.html`).
  - [ ] **Hybrid shape** (owner decision D2): head, `PageShell`, nav, breadcrumb and page-level framing are
        real Vue components using 23.2's primitives (`PageShell`, `ChartPanel`, `ListRow`, `StatusBadge`); the
        IR's rendered prose/chart HTML is injected with `v-html`. The proxy IR carries whole rendered HTML per
        page and **no view models**, so anything richer than this needs 22.2 — do not invent a parser.
  - [ ] Style injected content with `:deep()` scoped to the injecting component (CONVENTIONS.md §3). A plain
        scoped rule matches nothing and fails **silently**.

- [ ] **Task 5 — Head projection** (AC: #5)
  - [ ] Emit the golden head contract via `useHead`, sourced from the IR (title from the manifest entry;
        description/og from the IR or derived and documented). Match field-by-field against
        `PathUtil.RenderHeadOpen` (`PathUtil.cs:127–157`): charset, viewport, `<title>`, `description`,
        `og:type`/`og:title`/`og:description`, `<link rel="icon" href="{FaviconDataUri}">`, the
        `?v={AssetVersion}` cache-bust on both shared assets.
  - [ ] Record any field the IR cannot supply as a **named gap handed to 22.2** (the spike already listed a
        structured head/meta projection as a front-end ask) — do not silently drop it.

- [ ] **Task 6 — The IR-content stylesheet layer** (AC: #6)
  - [ ] Extend the 23.2 bridge: `web/scripts/extract-ir-content.mjs` produces `web/assets/ir-content.css` from
        `src/SpecScribe/assets/specscribe.css` — the class rules the four migrated families plus the
        pass-throughs actually use. Reuse `scripts/tokens-lib.mjs` patterns; keep the extraction a **copy**, no
        re-typed literals.
  - [ ] Add `npm run check:ir-content` as a drift gate beside `check:tokens`, and **prove it in both
        directions** the way 23.2 proved the token gate: observe it RED before extraction and RED on a
        hand-edited rule, not only green. A gate only ever seen passing is not a gate.
  - [ ] Apply it as a **global sheet scoped under the injecting wrapper** (e.g. `.ir-content { … }`
        descendants), so it cannot leak into template-authored components.
  - [ ] Emit an extraction **manifest** naming exactly which rule blocks are monolith-derived — this is the
        list 23.4 retires.
  - [ ] ⚠️ Never write the `*` + `/` sequence inside a CSS comment in any generated or hand-authored sheet —
        that exact mistake silently closed a comment and killed ~1,000 rules, invisible to the whole suite.
        Verify `document.styleSheets[i].cssRules.length` live, not by reading the source.

- [ ] **Task 7 — Boot the Hierarchy Explorer by reuse, not reimplementation** (AC: #7)
  - [ ] `web/scripts/sync-runtime-assets.mjs` copies `src/SpecScribe/assets/specscribe.js` (154 KB) and
        `plotly-hierarchy.min.js` (1.22 MB) into `web/public/assets/`, with a drift check like the token gate.
        Copy — never fork. ADR 0012 §Decision 2 makes one implementation the invariant.
  - [ ] Load both as client scripts on migrated routes. `specscribe.js` calls `initHierarchyExplorers(document)`
        at load (`specscribe.js:2379`); after any client-side content swap, dispatch the **existing** seam:
        `document.dispatchEvent(new CustomEvent('specscribe:content-swapped', { detail: { root: el } }))`
        (`specscribe.js:2380–2381`). No new API is needed and none should be added.
  - [ ] Reproduce the **anti-flash boot marker**. The `data-ss-hierarchy-boot` attribute on `<html>` is set by
        an inline chrome-level script (`HierarchyExplorer.cs:514`) that is deliberately **not** in the IR
        content region, so it will be missing under Nuxt; emit an equivalent from the Nuxt head. Confirm
        `.ss-hierarchy-booting` rules made it through the Task 6 extraction.
  - [ ] ⚠️ `v-html` **never executes** injected `<script>` tags. This is fine for the data island
        (`<script type="application/json" class="ss-hierarchy-data">`, `HierarchyExplorer.cs:415`) — it stays in
        the DOM as inert data, which is exactly what the component reads. It also means nothing executable can
        arrive through IR content, so the boot must come from the Nuxt layer.
  - [ ] Verify the **failure path**: with the bundle blocked, the server-rendered fallback SVG and the text twin
        remain visible and the page is unchanged (ADR 0013). The takeover handshake sets `data-explorer-ready`
        only on a successful mount — do not hide the SVG ahead of it.

- [ ] **Task 8 — Parity harness** (AC: #1)
  - [ ] `web/scripts/measure-parity.mjs`, modelled on `spike/nuxt-ir/scripts/measure.mjs`: extract the `<main>`
        region from golden / IR / Nuxt for each migrated surface, apply the golden-gate normalization (footer
        clock, `?v=<AssetVersion>`, product version, CRLF/BOM), and emit the three-column table plus a verbatim
        `emitted.includes(irMainInnerHtml)` check.
  - [ ] Cover **every** migrated surface, not a sample — the epics tree is ~27 epic pages plus the story pages.
        If you bound the comparison for runtime, **log what was dropped**; silent truncation reads as
        "covered everything".
  - [ ] Commit the measurement output. The 23.1 report's "every number is reproducible" claim was false at
        review time; do not repeat it.

- [ ] **Task 9 — Link-resolution harness** (AC: #4)
  - [ ] `web/scripts/check-links.mjs`: walk every emitted page, parse `<a href>`, skip external/anchor/mailto,
        resolve the rest **relative to the page's own path**, and assert the target exists in the prerendered
        output. Exit non-zero on a dangling internal target.
  - [ ] Report the counts (`total`, `internal`, `resolved`, `dangling`) so the number that closes the spike's
        open AC #1 half is on the record.

- [ ] **Task 10 — Accessibility and motion verification** (AC: #2)
  - [ ] Assert in the harness: exactly one `<main id="main-content">` and one skip link per page, and the skip
        link is the first focusable element.
  - [ ] Confirm the reduced-motion reduce block reaches **injected** content (it lives once, globally, in
        `assets/base.css` — check that the Task 6 layer does not reintroduce a per-rule duration that escapes it).
  - [ ] Confirm every status on the epics surfaces carries its word (UX-DR17), including inside injected markup.

- [ ] **Task 11 — Live browser verification** (AC: #1, #2, #4, #7 — CLAUDE.md § Verification)
  - [ ] Serve the prerendered output and the golden site side by side (add `.claude/launch.json` entries; 23.2's
        pattern — `web/.output/public` and `SpecScribeOutput`). Do **not** run servers via Bash.
  - [ ] Inspect **computed** styles and real DOM/scroll geometry, not source. The suite structurally cannot see
        containment leaks, sub-pixel collapse, or DOM corruption from markup splicing — all three have shipped
        here and all three were caught only by looking.
  - [ ] With JS **disabled**: the four surface families are readable and navigable; links resolve; charts show
        the fallback SVG + text twin.
  - [ ] With JS **enabled**: the Hierarchy Explorer mounts, drill-in works, and no flash-then-swap occurs.
  - [ ] Mobile pass — the page body must never scroll sideways; wide content scrolls inside its own container
        (23.2 found the token grid overflowing this way).

- [ ] **Task 12 — Documentation and story record** (AC: #3, #6, #8)
  - [ ] Extend `web/CONVENTIONS.md`: the region split and the nested-`<main>` trap; routes-mirror-IR-paths and
        why hrefs are never rewritten; the `ir-content.css` layer, its manifest, and that it is transitional;
        the runtime-asset copy and the reuse-not-reimplement rule.
  - [ ] Record in the story: the parity table, the link-resolution counts, the payload measurement, the
        enumerated deltas with causes, and the head fields the IR could not supply (handed to 22.2).
  - [ ] Update `sprint-status.yaml` **and** `epics.md` in the same change if any structural scope drift is
        recorded (CLAUDE.md — a change recorded in only one artifact is a drift bug). This story's ACs 3–8
        extend the epic's two; note that drift explicitly.

## Dev Notes

### Owner decisions locked at create-story (do not re-litigate)

1. **IR source: proxy behind an adapter.** 22.2 is `ready-for-dev`, not done. 23.3 consumes the shipped
   `SpaDelivery` output as a proxy IR through one adapter — the same move 22.1 and 23.1 made. 23.3 is **not**
   blocked on 22.2.
2. **Render shape: hybrid.** Component chrome (head, shell, nav, breadcrumb, page framing) + `v-html` content.
   Full component reimplementation is **not available** — the proxy IR has no view models.
3. **Link graph: full manifest route table.** Every page in the manifest prerenders; the four migrated families
   get the component treatment, everything else is a pass-through so navigability is provable end-to-end.
4. **Surface scope: dashboard + `epics.html` + per-epic pages + story detail pages.**
5. **Injected-content styling: a bounded, generated, drift-gated `ir-content.css`** — explicitly *not* a
   wholesale `specscribe.css` import (which would reverse 23.2's central decision) and *not* hand-authored
   `:deep()` rules for ~7,000 lines.
6. **Chart boot: port it now.** The migrated dashboard must be interactively equivalent, by **reusing** the
   shipped `initHierarchyExplorers` through the existing `specscribe:content-swapped` seam. ADR 0012
   §Decision 2 makes a second implementation the thing to avoid — so "port" here means *host it*, not
   *rewrite it in Vue*. If a live check shows reuse is genuinely impossible, stop and raise it rather than
   authoring a Vue sunburst.

### The five traps, in the order you will hit them

1. **`contentHtml` carries nav + breadcrumb + the `<main>` element itself.** `PageShell` already emits
   `<main id="main-content">` and the skip link. Naive injection ⇒ nested `<main>`, duplicate `id`, two navs.
   Split in the adapter (Task 2). The 23.1 spike never hit this because `SurfaceShell.vue` had no `<main>` of
   its own — do not copy that shape into `PageShell`.
2. **`tokens.css` styles none of the injected markup.** The Nuxt app imports only `tokens.css` + `base.css`
   (`nuxt.config.ts:18`); the IR's markup is authored against a 298 KB / 7,041-line stylesheet. Without Task 6
   every migrated page renders structurally correct and visually bare. The spike hid this by importing the
   monolith wholesale.
3. **`v-html` does not execute `<script>`.** Nothing executable can reach the page through IR content — hence
   Task 7. The JSON data island survives as inert DOM data, which is what the component wants.
4. **The boot marker is chrome-level, not content-level.** `HierarchyExplorer.cs:514`'s inline script is
   deliberately excluded from the region the webview/SPA consume, so it is absent under Nuxt and the anti-flash
   handshake silently degrades to a visible flash.
5. **`crawlLinks: true` aborts the build.** Already `false` in `nuxt.config.ts:31`. Keep it. The route table
   comes from the manifest, which is the correct design anyway.

### The proxy IR is lossy — do not chase these

Two deltas are **pre-existing defects in shipped C# code**, reproducible without Nuxt, and **owned by
Story 22.2**. Document them in the parity report as known-inherited; do not fix them here (22.2 is being
edited in the working tree right now — see Concurrency).

- **Dashboard `<main>`, 277 B / 5 anchors** (3 of them `code/*.html` links from the git-pulse panel). Root
  cause, verified: the static path passes `codeItemHref: CodeItemHref` into `HtmlTemplater.BuildIndexPage`,
  while the SPA and webview call sites use **named arguments starting at `counts:`** and silently skip the
  positional `codeItemHref` ⇒ `Charts.GitPulsePanel(pulse, null)` ⇒ link degrades to plain text. Golden
  `<main>` has 553 anchors; the IR has 548.
- **The page-local nav context band** (`site-nav-local-context`) that the static renderer computes and the
  capture discards, replaced by the generic key-views nav.

So: **`index.html` cannot reach byte parity through this proxy IR.** State that as an inherited delta with a
named cause — that is what AC #1's "documented-equivalent" is for. The three prose-shaped families should
reach byte-identical, as they did in the spike.

### What the IR actually gives you

`spa/manifest.json`:

```
{ siteTitle, entry, nav: [{ label, outputRelativePath }],
  pages: { "<outputRelativePath>": { title, chunk, breadcrumb: [{ label, outputRelativePath }],
                                     parent, children: [ … ] } } }
```

`spa/pages-{key}.json` — a flat `{ outputRelativePath: contentHtml }` map. Chunking groups by top-level output
segment, capped at 75 pages / 2 MB (`SpaDelivery.cs:37,56`), so `epics/**` lands in its own chunk(s).

Page paths you need: `index.html`; `epics.html`; `epics/epic-{N}.html` (`EpicsViewBuilder.cs:64`);
`epics/story-{id}.html` with dots replaced by dashes (`StoryEpicLinkifier.StoryPagePath`, `:46`).

### Components available from 23.2 (real APIs — do not invent props)

- `PageShell` — props `title`, `subtitle?`, `brand?`; slots default, `nav`, `footer`. **Owns the skip link and
  `<main id="main-content">`.**
- `StatusBadge` — props `stage` (`pending|drafted|ready|active|review|done|deferred|retired|unrecognized`),
  `label` (**required** — UX-DR17 enforced by shape), `meaning?`. Deliberately carries **no** stage→word map;
  that vocabulary belongs to the data.
- `ChartPanel` — props `title`, `window?`, `ranking?`, `note?`, `why?`; slots default, `legend`. Render order
  head → ranking → note → body → why, matching `Charts.Framed`.
- `ListRow` — props `summary`, `accent?`, `chips?`, `primaryHref?`, `primaryLabel?`, `resolved?`; slot `badge`.

### Payload discipline (23.2 AC #4, measured)

| variant | total | vs control |
| --- | --- | --- |
| A — `useAsyncData` | 170.0 KB | 1.36× |
| B — `.server.vue` island | 250.0 KB | 1.99× |
| **C — build-time, module scope** | **125.4 KB** | **1.00×** |

Use C. The island shape **amplifies** payload for prerendered content (it re-emits the rendered HTML *and* its
scoped CSS into `__nuxt_island/<Component>_<hash>.json`). AC #8 holds you to it.

### Scope guards

- **No production C# change.** `GoldenContentFingerprint` must **not** move. If it does, this story leaked into
  the renderer — revert, do not re-bless. (This is the one story in Epic 23 so far where a stationary
  fingerprint is the assertion rather than a hazard.)
- `web/` stays out of `SpecScribe.slnx` and out of `specscribe generate`. Packaging is 23.5.
- `spike/nuxt-ir/` stays as the throwaway 23.1 probe — read it, copy patterns, do not revive it.
- The pass-through catch-all is **not** a migration claim. Say so in code and in the story record; 23.4 owns it.
- **No new npm dependencies.** `web/` runs on `nuxt` + `vue` + `vue-router` and the vendored
  `plotly-hierarchy.min.js` that already ships. A markup parser, a CSS parser, or a link checker pulled from
  npm to satisfy Tasks 6/8/9 would add a dependency to a project holding a deliberate zero-dep posture
  (ADR 0010) — write the harnesses against Node built-ins and plain regex/string work, the way
  `scripts/tokens-lib.mjs` and `measure-payload.mjs` already do.

### ADR trigger — assess and propose (CLAUDE.md § Decision records)

Two decisions in this story may be cross-cutting contracts rather than story-local choices, and CLAUDE.md
requires proposing an ADR **without being asked** for those. Story 23.1 was corrected in code review for
checking the trigger against only one ADR, so check deliberately:

- **"Nuxt routes are the IR's output-relative paths verbatim."** This constrains 22.2's path scheme, 23.4's
  remaining surfaces, and 23.5's packaging — it is the reason no href is rewritten anywhere. Likely ADR-worthy.
- **`ir-content.css` as a transitional monolith-derived layer.** Bounded and self-retiring, but it is a second
  generated bridge beside the token bridge and it partially reverses the "tokens only" posture 23.2 set.

Read `docs/adrs/` first — a ratified ADR outranks memory and outranks this note. If either qualifies, propose
the ADR (0001–0015 are taken; 0016 is claimed by Story 22.2, so check before numbering) rather than burying the
decision in this story file.

### Concurrency (CLAUDE.md § Concurrent work on shared main)

Assume another session is editing these files right now:

- `_bmad-output/implementation-artifacts/22-2-*.md` and `sprint-status.yaml` are **modified in the working tree
  at baseline `261b300`** — 22.2 is actively in flight, and it owns both IR defects above.
- Epic 20 is mid-flight around exactly the assets Task 7 copies: **20.5 `review`** (uncommitted verify-round
  work has landed in the tree before), **20.6 / 20.7 / 20.8 `ready-for-dev`**, **20.9 `backlog`**. `20.7`
  deletes the three legacy arc renderers and `20.9` finishes the rollout. So `specscribe.js`,
  `specscribe.css` and `HierarchyExplorer.cs` **will move under you**.
- Therefore: **copy those assets through a script with a drift check**, never fork them, and re-run the copy
  before verifying. **Grep-verify every symbol you add before relying on it** — a `Charts.cs` edit has silently
  vanished this way before.
- Never `git reset --hard`, `git checkout --`, or `git clean`.

### Verification

- Generate to `SpecScribeOutput/` (the default). Never `--output docs/live` — vestigial and gitignored.
- Verify in a **live browser**, both JS-on and JS-off, inspecting computed styles and real geometry.
- Every chart needs an accessible text equivalent, and no state may be signalled by color alone.
- Expect one rotating file-write-contention flake per full C# suite run (23.2 recorded six different tests
  across runs, all passing in isolation). Report it honestly rather than as a clean pass — though note this
  story should not need the C# suite to change at all.

### Project Structure Notes

- **New — `web/`:** `ir/adapter.ts`; `pages/[...path].vue` (the single catch-all — see Task 3) plus one
  surface component per migrated family under `components/surfaces/`; `assets/ir-content.css` *(generated)*;
  `public/assets/specscribe.js` + `plotly-hierarchy.min.js` *(copied)*;
  `scripts/extract-ir-content.mjs`, `check-ir-content.mjs`, `sync-runtime-assets.mjs`, `measure-parity.mjs`,
  `check-links.mjs`; `measurements/` output.
- **Update — `web/`:** `nuxt.config.ts` (manifest-driven `nitro.prerender.routes`, the new css entry),
  `package.json` (new scripts), `CONVENTIONS.md`, `README.md`, `components/PageShell.vue` (nav slot use,
  `<main>` attribute parity).
- **Update — tooling:** `.claude/launch.json` (preview servers for `web/.output/public` and `SpecScribeOutput`).
- **Unchanged:** `src/SpecScribe/**`, `tests/**`, `SpecScribe.slnx`, `spike/nuxt-ir/**`.

### References

- [Epic 23 + Story 23.3 ACs](../planning-artifacts/epics.md) — §Epic 23, §Story 23.3; execution order
  23.2 → 23.3 → 23.5 → 23.4.
- [Story 23.1 spike report](23-1-spike-report.md) — Axis 1 (navigability narrowed, the open half this story
  closes), Axis 2 (parity table, the three enumerated deltas, the `:deep()` surprise), findings 3, 4, 7, 8;
  gate row 23.3.
- [Story 23.2](23-2-component-library-and-design-token-bridge.md) — primitives, the token bridge and its
  both-directions drift proof, the payload measurement.
- [web/CONVENTIONS.md](../../web/CONVENTIONS.md) — §1 tokens are generated, §3 `:deep()`, §4 build-time data,
  §5 one vocabulary, §6 a11y/motion.
- [ADR 0009](../../docs/adrs/0009-frontend-framework-for-projection-layer.md) — Vue + Nuxt 3, universal/SSR,
  full prerender. [ADR 0012](../../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md)
  §Decision 2 — one Hierarchy Explorer is the only route to a sunburst or treemap.
  [ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md) — the text twin is the no-JS contract.
  [ADR 0008](../../docs/adrs/0008-json-ir-canonical-delivery.md) — the IR 22.2 will build.
- [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md) — AD-1/AD-2 (no disk read-back),
  AD-7 (presentation tokens shared; host chrome host-owned).
- C# seams: `src/SpecScribe/SpaDelivery.cs` (`ExtractContentRegion` :76, manifest shape :231–246, caps :37/:56);
  `src/SpecScribe/PathUtil.cs` (`RenderHeadOpen` :127); `src/SpecScribe/HierarchyExplorer.cs` (data island :415,
  boot marker :514); `src/SpecScribe/assets/specscribe.js` (`initHierarchyExplorers` :1709, auto-init :2379,
  re-init seam :2380); `src/SpecScribe/EpicsViewBuilder.cs` :64; `src/SpecScribe/StoryEpicLinkifier.cs` :46.
- Memory: `story-23-2-component-library-token-bridge-done`, `story-23-1-nuxt-over-ir-spike-seeded`,
  `story-20-5-hierarchy-explorer-done`, `story-20-2-zoomable-drill-in-done` (the
  `specscribe:content-swapped` seam), `css-comment-star-slash-silent-truncation`,
  `shared-main-concurrent-edit-loss-verify-after-edit`, `generate-output-dir-is-specscribeoutput`,
  `charting-is-pure-svg-no-js` (superseded here by ADR 0012/0013).

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

## Change Log

| Date | Change |
| --- | --- |
| 2026-07-26 | Story 23.3 created (baseline `261b300`). Six owner decisions locked: proxy IR behind one adapter; hybrid component-chrome + `v-html` shape; full manifest route table with routes mirroring IR paths verbatim; surface scope = dashboard + epics index + per-epic + story pages; a bounded generated drift-gated `ir-content.css` for injected markup; and porting the Hierarchy Explorer boot now by reusing the shipped implementation. ACs 3–8 extend the epic's two. |
