# `web/` — SpecScribe's Vue/Nuxt presentation layer

The production-intent Nuxt 3 app for Epic 23. Established by Story 23.2 (component library + design-token
bridge); Story 23.3 migrates the first real surfaces onto it.

**Not shipped yet.** This app is deliberately not in `SpecScribe.slnx` and not wired into
`specscribe generate` — how the Node build reaches users is Story 23.5's decision, which is sequenced ahead
of 23.4 for that reason.

## Getting started

```bash
npm install
npm run extract:tokens   # generate assets/tokens.css from the C# stylesheet
npm run dev              # http://localhost:3000
```

## Scripts

| script | what it does |
| --- | --- |
| `npm run dev` | Dev server with HMR. |
| `npm run generate` | Full static prerender to `.output/public` (ADR 0009 Option B). |
| `npm run extract:tokens` | Regenerate `assets/tokens.css` from `src/SpecScribe/assets/specscribe.css`. |
| `npm run check:tokens` | Drift gate — fails when the generated tokens and the C# source diverge. |
| `npm run measure:payload` | Story 23.2 AC #4's hydration-payload experiment (run after `generate`). |

## Layout

```
assets/tokens.css        GENERATED — verbatim copy of the C# stylesheet's :root block. Never hand-edit.
assets/base.css          The app's own minimal base layer (reset, typography, focus, reduced-motion).
components/              The shared primitives: StatusBadge, ChartPanel, ListRow, PageShell.
pages/design-system.vue  The design system: every primitive in every state. The library's own consumer.
pages/measure/           AC #4's three data-path variants. Measurement fixtures, not product surfaces.
scripts/                 The token bridge (extract + drift check) and the payload measurement harness.
```

## Read this before authoring a component

[CONVENTIONS.md](CONVENTIONS.md) — token discipline, scoped-SFC rules, the `:deep()` requirement for
`v-html`'d content, and the measured recommendation on how data should reach a component.
