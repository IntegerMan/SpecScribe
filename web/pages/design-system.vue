<script setup lang="ts">
/**
 * The design-system route — every primitive in every relevant state.
 *
 * This page is the component library's own consumer. Building it alongside the primitives is what keeps
 * them from being authored blind: every prop below was exercised before the component was called done, and
 * 23.3 inherits components that have at least one real call site.
 *
 * The status vocabulary lives HERE, as page data, not inside StatusBadge. The components are purely
 * presentational; what statuses exist is the data layer's business (C# `StatusStyles` today, the canonical
 * IR from 23.3 on). See CONVENTIONS.md, "One vocabulary, one owner".
 */
import type { StatusStage } from '~/components/StatusBadge.vue'

useHead({
  title: 'Design System — SpecScribe',
  meta: [
    {
      name: 'description',
      content:
        "SpecScribe's shared design system: the status and motion token families, and the visual primitives every surface is built from.",
    },
  ],
})

/** Mirrors StatusStyles.LegendStages + its label/meaning seams. Ordered as the portal's legend teaches them. */
const stages: { stage: StatusStage; label: string; meaning: string }[] = [
  { stage: 'pending', label: 'Pending', meaning: 'Not yet ready to pick up' },
  { stage: 'drafted', label: 'Drafted', meaning: 'Stories or a plan exist; work has not started' },
  { stage: 'ready', label: 'Ready for dev', meaning: 'Task plan exists and dependencies met' },
  { stage: 'active', label: 'In development', meaning: 'Actively being developed' },
  { stage: 'review', label: 'In review', meaning: 'Implementation complete; awaiting review or retrospective' },
  { stage: 'done', label: 'Done', meaning: 'Finished and closed' },
  { stage: 'deferred', label: 'Deferred', meaning: 'Shelved on purpose for later' },
  { stage: 'retired', label: 'Retired', meaning: 'Removed from the active plan; kept for ledger history' },
  { stage: 'unrecognized', label: 'Unrecognized', meaning: 'Native status word has no canonical mapping' },
]

/** The motion vocabulary. Roles, not durations — the value is whatever the token says it is. */
const motion = [
  { token: '--motion-fast', role: 'Hover and opacity feel — the shortest deliberate change.' },
  { token: '--motion-entrance', role: 'The standard reveal: panels, charts, cards.' },
  { token: '--motion-entrance-long', role: 'Sweeps that travel a distance, such as a progress bar filling.' },
  { token: '--motion-ease', role: 'The one easing curve every entrance shares.' },
  { token: '--motion-stagger', role: 'Per-item delay unit when a group enters in sequence.' },
]

/** A fragment standing in for content the app does not author — the shape 23.3 injects from the IR. */
const injectedHtml = `<p class="injected-note">This paragraph arrived as an HTML <em>string</em> and was
  injected with <code>v-html</code>. Vue did not author its elements, so it carries no
  <code>data-v-*</code> attribute.</p>`
</script>

<template>
  <PageShell
    title="Design System"
    subtitle="The tokens and primitives every SpecScribe surface is built from. Colour is always reinforcement — every status is also a word."
  >
    <ChartPanel
      title="Status tokens"
      ranking="Nine canonical stages, in the order the portal teaches them."
      why="One token per lifecycle stage means a stage reads as the same colour on every chart, legend, badge and row — and changing it changes all of them at once."
    >
      <ul class="swatch-grid">
        <li v-for="s in stages" :key="s.stage" class="swatch-row">
          <span class="swatch" :class="`swatch-${s.stage}`" aria-hidden="true" />
          <code class="swatch-token">--status-{{ s.stage }}</code>
          <StatusBadge :stage="s.stage" :label="s.label" :meaning="s.meaning" />
          <span class="swatch-meaning">{{ s.meaning }}</span>
        </li>
      </ul>
      <template #legend>
        <p class="panel-aside">
          A requirement that is listed but mapped to no epic reuses the <code>pending</code> swatch and
          carries its own word, <em>Unmapped</em> — the colour is shared, the meaning is not.
        </p>
      </template>
    </ChartPanel>

    <ChartPanel
      title="Motion tokens"
      note="Every duration below is neutralised under prefers-reduced-motion by one global rule — no component declares its own reduced-motion block."
      why="Naming the timings makes motion a vocabulary rather than a scattering of magic numbers, so the whole portal accelerates and settles with one feel."
    >
      <dl class="motion-list">
        <div v-for="m in motion" :key="m.token" class="motion-row">
          <dt><code>{{ m.token }}</code></dt>
          <dd>{{ m.role }}</dd>
        </div>
      </dl>
      <div class="motion-demo">
        <span class="motion-demo-label">Entrance, at <code>--motion-entrance</code>:</span>
        <span class="motion-demo-bar" />
      </div>
    </ChartPanel>

    <ChartPanel
      title="Status badge"
      window="StatusBadge.vue"
      why="A badge's label is a required prop, so a status can never be shown by colour alone."
    >
      <div class="badge-row">
        <StatusBadge v-for="s in stages" :key="s.stage" :stage="s.stage" :label="s.label" :meaning="s.meaning" />
      </div>
    </ChartPanel>

    <ChartPanel
      title="List row"
      window="ListRow.vue"
      ranking="Summary, optional badge, metadata chips, one primary link — plus the accent and resolved states."
      why="One row anatomy across requirements, epics, ADRs and timelines means a reader learns to scan once."
    >
      <ul class="row-list">
        <ListRow summary="A row with nothing but a summary." />
        <ListRow
          summary="A row carrying a status badge, two chips, and its one primary affordance."
          accent="review"
          :chips="['2026-07-25', '3 tasks']"
          primary-href="#"
          primary-label="Open story"
        >
          <template #badge>
            <StatusBadge stage="review" label="In review" meaning="Implementation complete; awaiting review or retrospective" />
          </template>
        </ListRow>
        <ListRow summary="A deferred row — the accent reinforces the badge, it never replaces it." accent="deferred">
          <template #badge>
            <StatusBadge stage="deferred" label="Deferred" meaning="Shelved on purpose for later" />
          </template>
        </ListRow>
        <ListRow summary="A resolved row de-emphasises and takes the done accent." resolved :chips="['closed']" />
      </ul>
    </ChartPanel>

    <ChartPanel
      title="Framed panel"
      window="ChartPanel.vue"
      ranking="Every panel on this page is one — including this one."
      note="A note is a caveat about the DATA. The italic line below is the generic framing sentence. They are different slots because they answer different questions."
      why="Framing every chart the same way means a reader never has to work out what they are looking at from the chart alone."
    >
      <p class="panel-body-demo">
        The body slot holds whatever the panel frames — a chart, a table, a list. Slots that are not filled
        render nothing at all, so a panel never carries an empty heading.
      </p>
    </ChartPanel>

    <ChartPanel
      title="Styling injected content"
      window="the :deep() convention"
      note="This is the single most load-bearing convention on this page for Story 23.3, which injects the IR's rendered prose."
      why="Scoped styles are scoped to what the TEMPLATE authored. Content that arrives as a string is outside that boundary, and reaching it needs a deliberate escape hatch."
    >
      <div class="deep-demo">
        <div class="deep-case">
          <h4>Plain <code>scoped</code> — does not reach</h4>
          <div class="scoped-only" v-html="injectedHtml" />
          <p class="deep-verdict">
            The rule <code>.scoped-only .injected-note</code> compiles to a <code>[data-v-*]</code> selector.
            Vue stamps that attribute onto template-authored elements only, so the injected paragraph never
            matches and stays unstyled.
          </p>
        </div>
        <div class="deep-case">
          <h4><code>:deep()</code> — reaches</h4>
          <div class="with-deep" v-html="injectedHtml" />
          <p class="deep-verdict">
            <code>.with-deep :deep(.injected-note)</code> drops the attribute requirement on the descendant,
            so the same markup picks up the rule. This — or a global sheet — is the only way to style
            <code>v-html</code>'d content.
          </p>
        </div>
      </div>
    </ChartPanel>
  </PageShell>
</template>

<style scoped>
.swatch-grid,
.row-list {
  list-style: none;
  margin: 0;
  padding: 0;
}

.swatch-row {
  display: grid;
  grid-template-columns: 1.6rem 11rem 9rem 1fr;
  align-items: center;
  gap: 0.6rem;
  padding: 0.3rem 0;
}

.swatch {
  width: 1.4rem;
  height: 1.4rem;
  border-radius: 3px;
  border: 1px solid var(--border);
}

/* Each swatch shows its token's actual value by USING it. Nothing here re-types a colour, which is also why
   the page cannot claim a value the portal does not render. */
.swatch-pending { background: var(--status-pending); }
.swatch-drafted { background: var(--status-drafted); }
.swatch-ready { background: var(--status-ready); }
.swatch-active { background: var(--status-active); }
.swatch-review { background: var(--status-review); }
.swatch-done { background: var(--status-done); }
.swatch-deferred { background: var(--status-deferred); }
.swatch-retired { background: var(--parchment-dark); }
.swatch-unrecognized {
  background: repeating-linear-gradient(
    -45deg,
    var(--warm-white),
    var(--warm-white) 3px,
    var(--status-unrecognized-hatch) 3px,
    var(--status-unrecognized-hatch) 6px
  );
  border: 1px dashed var(--status-unrecognized);
}

.swatch-token,
.swatch-meaning {
  font-size: 0.76rem;
  color: var(--ink-faded);
}

.badge-row {
  display: flex;
  flex-wrap: wrap;
  gap: 0.45rem;
}

.motion-list {
  margin: 0;
}

.motion-row {
  display: grid;
  grid-template-columns: 13rem 1fr;
  gap: 0.6rem;
  padding: 0.25rem 0;
  font-size: 0.82rem;
}

.motion-row dd {
  color: var(--ink-faded);
}

/* Narrow viewports: both reference grids stack instead of scrolling sideways inside their panel. The panel's
   own `overflow-x: auto` already stopped a wide grid from pushing the PAGE sideways, but "the reader can
   scroll a table off-screen to find the badge" is a worse answer than "the row stacks". */
@media (max-width: 34rem) {
  .swatch-row {
    grid-template-columns: 1.6rem 1fr;
    grid-template-areas:
      'swatch token'
      'badge  badge'
      'meaning meaning';
    row-gap: 0.2rem;
    padding: 0.55rem 0;
    border-bottom: 1px solid var(--border);
  }

  .swatch-row > .swatch { grid-area: swatch; }
  .swatch-row > .swatch-token { grid-area: token; }
  .swatch-row > .status-badge { grid-area: badge; justify-self: start; }
  .swatch-row > .swatch-meaning { grid-area: meaning; }

  .motion-row {
    grid-template-columns: 1fr;
    row-gap: 0.1rem;
  }
}

.motion-demo {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-top: 1rem;
  font-size: 0.78rem;
  color: var(--ink-faded);
}

.motion-demo-bar {
  display: block;
  height: 0.6rem;
  width: 14rem;
  border-radius: 999px;
  background: var(--status-active);
  transform-origin: left center;
  animation: sweep var(--motion-entrance-long) var(--motion-ease) both;
}

@keyframes sweep {
  from { transform: scaleX(0); }
  to { transform: scaleX(1); }
}

.panel-aside,
.panel-body-demo,
.deep-verdict {
  font-size: 0.8rem;
  color: var(--ink-faded);
  margin: 0;
  line-height: 1.5;
}

.deep-demo {
  display: grid;
  gap: 1.2rem;
  grid-template-columns: repeat(auto-fit, minmax(17rem, 1fr));
}

.deep-case h4 {
  font-size: 0.76rem;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: var(--ink-light);
  margin-bottom: 0.5rem;
}

.scoped-only,
.with-deep {
  border: 1px dashed var(--border);
  border-radius: 4px;
  padding: 0.6rem 0.75rem;
  margin-bottom: 0.5rem;
  font-size: 0.82rem;
}

/* The failing case, on purpose. This selector compiles to `.scoped-only .injected-note[data-v-*]`, and the
   injected paragraph carries no such attribute — so nothing below applies to it. Left in as the control. */
.scoped-only .injected-note {
  color: var(--rust);
  font-weight: 700;
  background: var(--parchment-dark);
}

/* The working case. `:deep()` drops the attribute requirement on the descendant part of the selector. */
.with-deep :deep(.injected-note) {
  color: var(--teal-deep);
  border-left: 3px solid var(--status-active);
  padding-left: 0.6rem;
}
</style>
