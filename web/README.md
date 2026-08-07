# `web/` — SpecScribe's Vue/Nuxt presentation layer

The production-intent Nuxt app for Epic 23 (Nuxt 4 — `package.json` pins `^4.5.1`; it was established on
Nuxt 3 and the wording lagged the bump). Established by Story 23.2 (component library + design-token
bridge); Story 23.3 migrated the first real surfaces onto it — the dashboard and the whole epics tree — and
prerenders the rest of the site as pass-throughs so the link graph resolves end to end.

**Not shipped yet.** This app is deliberately not in `SpecScribe.slnx` and not wired into
`specscribe generate` — how the Node build reaches users is Story 23.5's decision, which is sequenced ahead
of 23.4 for that reason.

## Getting started

It reads the generated IR, so generate one first:

```bash
dotnet run --project ../src/SpecScribe -- generate --spa
```

That writes `SpecScribeOutput/` — both the static site (the parity oracle) and `spa/manifest.json` +
`spa/pages-*.json` (the IR). Point `SPECSCRIBE_IR_DIR` elsewhere to use another checkout's output.

```bash
npm install
npm run extract:tokens       # generate assets/tokens.css from the C# stylesheet
npm run extract:ir-content   # generate assets/ir-content.css for the injected markup
npm run dev                  # http://localhost:3000
```

`npm run dev`, `build` and `generate` all re-run the runtime-asset copy first, so `specscribe.js` and the
charting engine are always current — Epic 20 moves those files often.

## Scripts

| script | what it does |
| --- | --- |
| `npm run dev` | Dev server with HMR. |
| `npm run generate` | Full static prerender to `.output/public` (ADR 0009 Option B). ~1,055 routes, ~15 s. |
| `npm run check` | All three drift gates: tokens, IR-content stylesheet, runtime assets. |
| `npm run extract:tokens` / `check:tokens` | The token bridge and its gate (Story 23.2 AC #1). |
| `npm run extract:ir-content` / `check:ir-content` | The IR-content stylesheet layer and its gate (ADR 0018). |
| `npm run sync:assets` / `check:assets` | Copy the portal's own JS/CSS runtime assets, and verify the copy. |
| `npm run measure:parity` | `<main>` byte parity per migrated surface, golden → IR → emitted (23.3 AC #1). |
| `npm run check:links` | Internal link resolution vs the golden site, link-for-link (23.3 AC #4). |
| `npm run check:a11y` | Landmark, skip-link, `lang`, status-word and motion assertions (23.3 AC #2). |
| `npm run measure:payload` | Story 23.2 AC #4's hydration-payload experiment. |

The four measurement/verification scripts need `npm run generate` to have run, and they refuse to publish a
number from a truncated prerender (see `SPECSCRIBE_IR_ROUTE_LIMIT` below). Their output is committed under
`measurements/`.

### `SPECSCRIBE_IR_ROUTE_LIMIT`

A dev knob: prerender only the first N IR routes, so iterating on the pipeline does not cost a whole site
each time. Unset means all of them. Every harness hard-fails when it is set, because a partial run reads
exactly like a complete one once its output is pasted into a story file.

## Layout

```
ir/adapter.ts            The ONE file that knows the IR's field names. Reads it at BUILD TIME.
ir/adapter.client.ts     The browser stub `#ir` resolves to. Throws — nothing here should ever run.
ir/types.ts              The neutral shape both of the above speak. Types only, compiles to nothing.

pages/[...path].vue      The single catch-all. All IR routing goes through it (ADR 0017).
pages/component-library  The app's own dev landing page. `/` is the project dashboard.
pages/measure/           23.2 AC #4's three data-path variants. Measurement fixtures, not product surfaces.
error.vue                The error page, and the reason 404s carry a landmark and a skip link.

components/              The shared primitives: StatusBadge, ChartPanel, ListRow, PageShell.
components/IrHtml.ts     Injects a run of IR markup with NO wrapper element.
components/IrMain.ts     The `<main>` landmark for IR routes, with no scoped attribute and no fragment anchors.
components/surfaces/     One component per migrated family, plus the shared IrSurface and the pass-through.

assets/tokens.css            GENERATED — verbatim copy of the C# stylesheet's :root blocks. Never hand-edit.
assets/ir-content.css        GENERATED — the bounded, SCOPED monolith extract for injected markup. Transitional.
assets/shared-primitives.css GENERATED — the bounded UNSCOPED layer (ADR 0029). Reaches template-authored
                             components too, which is the whole point. Allowlist: `pill`, `skip-link`.
assets/runtime-body.css      The runtime-attached body-level classes (ADR 0039).
assets/base.css              The app's own minimal base layer (reset, typography, focus, reduced-motion).

server/plugins/          Two Nitro plugins: real prerender error messages, and no payload for IR routes.
scripts/                 The two bridges, the asset copy, and the three verification harnesses.
measurements/            Committed harness output. Story 23.1 claimed reproducible numbers and wasn't.
```

## Read this before authoring a component

[CONVENTIONS.md](CONVENTIONS.md) — token discipline, scoped-SFC rules, the `:deep()` requirement for
`v-html`'d content, the measured recommendation on how data should reach a component, and — added by 23.3 —
the route-space contract, the region split and its nested-`<main>` trap, the `ir-content.css` layer, and the
copy-never-fork rule for runtime assets.
