# `spike/nuxt-ir` — Story 23.1 Nuxt-over-IR feasibility probe

**Throwaway.** Nothing here ships. No `.slnx` references it, it is not part of `dotnet build src/SpecScribe`,
`dotnet pack`, the site build, or the `extension/` bundle. The generated `specscribe` site is byte-identical
with or without this folder. Deletable with the branch.

The durable output is
[`_bmad-output/implementation-artifacts/23-1-spike-report.md`](../../_bmad-output/implementation-artifacts/23-1-spike-report.md).
This code is the evidence behind it.

## What it is

A minimal **Nuxt 3** app (universal/SSR, fully prerendered — ADR 0009's ratified Axis 1 = Option B) that renders
four real SpecScribe surfaces from the **proxy IR**. Epic 22's canonical IR (Story 22.2) does not exist yet, so —
exactly as Story 22.1 did — this consumes the already-shipped `SpaDelivery` output (`spa/manifest.json` +
`spa/pages-*.json`) instead.

The four surfaces are chosen so that between them they exercise all three AC #2 sub-risks:

| route | IR page | probes |
| --- | --- | --- |
| `/dashboard` | `index.html` | chart-SVG injection (148 inline SVGs), status/motion tokens |
| `/adr-0006` | `adrs/0006-…html` | Markdig: Mermaid ×5, gherkin, capability, tables |
| `/story-22-1` | `implementation-artifacts/22-1-…html` | Markdig: reference chips ×34, comment annotations |
| `/ux-design` | `planning-artifacts/ux-designs/…/DESIGN.html` | Markdig: colour swatches ×48, tables |

## Layout

| path | role |
| --- | --- |
| `ir/adapter.mjs` | **The only file that knows the IR's shape.** SpaDelivery → neutral `IrPage`. When Story 22.2 lands a real schema, this is what changes. |
| `scripts/extract-ir.mjs` | Build step: pulls the chosen surfaces out of the ~82 MB proxy IR into `ir-data/ir.json`, and copies the shipped `specscribe.css` verbatim (never re-typed → no token drift, AD-7). |
| `scripts/csp-probe.mjs` | Static server that replays the **VS Code webview's exact CSP** (`WebviewRenderAdapter.cs:102`) over the prerendered output, substituting `__NONCE__` per request the way the webview host does. |
| `scripts/measure.mjs` | Regenerates every number in the report from the artifacts. |
| `server/plugins/csp-nonce.ts` | Stamps `nonce="__NONCE__"` onto every `<script>` Nuxt emits, via Nitro's `render:html` hook. |
| `server/api/ir.get.ts` | Serves one `IrPage` at prerender time, keyed by route path. |
| `pages/[...surface].vue` | Catch-all; the route table comes from the IR manifest, not from the filesystem. |
| `components/SurfaceShell.vue` | Scoped-SFC chrome + `v-html` injection of the IR's content. |
| `components/StatusLegend.vue` | The scoped-CSS + `--status-*`/`--motion-*` token probe. |

## Run it

```bash
cd spike/nuxt-ir && npm install
```

```bash
dotnet run --project ../../src/SpecScribe -c Release -- generate --spa
```

```bash
npm run ir && npm run generate && npm run measure
```

Then replay the webview CSP over the output (`webview` = the shipped policy verbatim; `strict-dynamic` and
`relaxed` are the two candidate relaxations; `off` is the control):

```bash
node scripts/csp-probe.mjs 5311 webview
```

Scaling probe — `SPIKE_SCALE=N npm run ir` adds the first N of the site's 914 real pages as routes, so
prerender throughput is measured rather than extrapolated. `SPIKE_NO_PAYLOAD_EXTRACTION=1 npm run generate`
inlines the hydration payload instead of emitting a sibling `_payload.json`.

## Headline findings (details in the report)

1. **NFR6 holds.** Prerendered HTML is fully rendered; with every `<script>` removed the dashboard still carries
   345 KB of content, 148 SVGs and 570 links, and each route is its own file on disk.
2. **Parity is byte-identical where it counts.** `<main>` is byte-for-byte equal across golden → IR → Nuxt for
   3 of 4 surfaces; `v-html` round-trips the IR string verbatim. The dashboard's 277-byte delta is introduced by
   **SpaDelivery**, not by Nuxt.
3. **The webview CSP blocks hydration today**, and the partial relaxation is worse than none — see the report's
   CSP matrix before touching `WebviewRenderAdapter`'s policy.
4. **Node costs ~37 s and ~2.26× output weight** at full site scale. The weight is *entirely* hydration payload;
   the prerendered HTML itself is within 1% of the C# static site.
