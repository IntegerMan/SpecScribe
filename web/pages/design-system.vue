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

/**
 * Mirrors StatusStyles.LegendStages + its label/meaning seams, in the order the portal's legend teaches them.
 *
 * ⚠️ All TEN stages. `unmapped` was missing until the 2026-07-28 re-review, and the aside that stood in for it
 * stated the word as "Unmapped" where `StatusStyles.LegendWord("unmapped")` renders **"Not yet mapped"** — so
 * the page whose subject IS the status vocabulary taught a word the portal does not use. Two stages have no
 * `--status-*` token of their own and borrow one; `tokenFor` below is the single place that is stated.
 */
const stages: { stage: StatusStage; label: string; meaning: string }[] = [
  { stage: 'pending', label: 'Pending', meaning: 'Not yet ready to pick up' },
  { stage: 'drafted', label: 'Drafted', meaning: 'Stories or a plan exist; work has not started' },
  { stage: 'ready', label: 'Ready for dev', meaning: 'Task plan exists and dependencies met' },
  { stage: 'active', label: 'In development', meaning: 'Actively being developed' },
  { stage: 'review', label: 'In review', meaning: 'Implementation complete; awaiting review or retrospective' },
  { stage: 'done', label: 'Done', meaning: 'Finished and closed' },
  { stage: 'deferred', label: 'Deferred', meaning: 'Shelved on purpose for later' },
  { stage: 'unmapped', label: 'Not yet mapped', meaning: 'Listed as a requirement but mapped to no epic' },
  { stage: 'retired', label: 'Retired', meaning: 'Removed from the active plan; kept for ledger history' },
  { stage: 'unrecognized', label: 'Unrecognized', meaning: 'Native status word has no canonical mapping' },
]

/**
 * Which `--status-*` token a stage actually paints with.
 *
 * ⚠️ The caption used to interpolate `--status-{{ stage }}` unconditionally, which published
 * **`--status-retired`** — a custom property declared in neither `tokens.css` nor `specscribe.css`. A
 * component author who followed the design-system page got an unstyled element. The C# twin has exactly this
 * guard (`DesignSystemTemplater.cs`); this is its mirror, and the two must be changed together.
 */
function tokenFor(stage: StatusStage): string {
  if (stage === 'unmapped') return '--status-pending'
  if (stage === 'retired') return '--status-deferred'
  return `--status-${stage}`
}

/**
 * The motion vocabulary. Roles, not durations — the value is whatever the token says it is.
 *
 * ⚠️ VERBATIM from `DesignSystemTemplater.MotionTokens`. The duplication is owner-accepted until 23.4 retires
 * the C# page; DIVERGENCE is not, and all five sentences had drifted apart on day one. If you edit one, edit
 * both — `SiteGeneratorDesignSystemTests` pins the C# side.
 */
const motion = [
  { token: '--motion-fast', role: 'Hover and opacity changes — the shortest deliberate movement on the page.' },
  { token: '--motion-entrance', role: 'The standard reveal, used by charts, panels and cards as they appear.' },
  { token: '--motion-entrance-long', role: 'Movement that travels a distance, such as a progress bar filling.' },
  { token: '--motion-ease', role: 'The single easing curve every entrance shares, so nothing feels out of place.' },
  { token: '--motion-stagger', role: 'The delay between items when a group enters one after another.' },
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
      ranking="Ten canonical stages, in the order the portal teaches them."
      why="One token per lifecycle stage means a stage reads as the same colour on every chart, legend, badge and row — and changing it changes all of them at once."
    >
      <ul class="swatch-grid">
        <li v-for="s in stages" :key="s.stage" class="swatch-row">
          <span class="swatch" :class="`swatch-${s.stage}`" aria-hidden="true" />
          <code class="swatch-token">{{ tokenFor(s.stage) }}</code>
          <StatusBadge :stage="s.stage" :label="s.label" :meaning="s.meaning" />
          <span class="swatch-meaning">{{ s.meaning }}</span>
        </li>
      </ul>
      <template #legend>
        <p class="panel-aside">
          Two stages have no token of their own and say so above: <em>Not yet mapped</em> shares
          <code>--status-pending</code> (it is a requirement-level state, not an eleventh lifecycle stage) and
          <em>Retired</em> shares <code>--status-deferred</code>. In both cases the colour is shared and the
          word is not — which is the whole point, because the vocabulary is carried by language.
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
      why="The word is always present, so a status stays legible in greyscale and to a screen reader. The stage colour is only ever reinforcement."
    >
      <div class="badge-row">
        <StatusBadge v-for="s in stages" :key="s.stage" :stage="s.stage" :label="s.label" :meaning="s.meaning" />
      </div>
    </ChartPanel>

    <ChartPanel
      title="List row"
      ranking="Summary, optional badge, metadata chips, one primary link — plus the accent and resolved states."
      why="One row anatomy across requirements, epics, ADRs and timelines means a reader learns to scan once."
    >
      <ul class="row-list">
        <ListRow summary="A row with nothing but a summary." />
        <ListRow
          summary="A row carrying a status badge, two chips, and its one primary affordance."
          accent="ready"
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
/* `unmapped` and `retired` borrow, exactly as the portal's own legend swatches do
   (`.status-legend-key-swatch.retired { background: var(--status-deferred) }`). This said
   `var(--parchment-dark)` until the 2026-07-28 re-review — a colour the portal never renders for Retired, and
   4/255 in one channel away from --status-drafted, so on the page whose subject IS the colour vocabulary
   Retired and Drafted were the same swatch. */
.swatch-unmapped { background: var(--status-pending); }
.swatch-retired { background: var(--status-deferred); }
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
