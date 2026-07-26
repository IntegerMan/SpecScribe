<script setup lang="ts">
/**
 * The framed chart panel — the Vue counterpart of `Charts.Framed(ChartMeta, …)` plus `.chart-panel` /
 * `.chart-frame-*` in specscribe.css (Story 10.2; the `note` slot is Story 10.6).
 *
 * The point of the C# helper is that title / window / ranking / note / why are metadata-consistent BY
 * CONSTRUCTION — no call site hand-writes the frame. Props here carry the same contract, in the same
 * render order, so a panel authored in Vue cannot grow a different anatomy from one authored in C#:
 *
 *   head (title + window) -> ranking -> note -> body -> why
 *
 * `legend` is a named slot rather than a prop because a legend is markup, not metadata.
 */
defineProps<{
  /** Required. The panel's name. */
  title: string
  /** Numeric analysis window ("last 90 days"). Omitted entirely when absent — never rendered empty (NFR8). */
  window?: string
  /** Ranked-list caption ("Top 10 by churn"). */
  ranking?: string
  /** A caveat about the DATA ("some pairs are process coupling, not a code dependency"). */
  note?: string
  /** Why this metric matters — generic framing, never project-specific (NFR8). */
  why?: string
}>()
</script>

<template>
  <section class="chart-panel">
    <div class="chart-frame-head">
      <h3>{{ title }}</h3>
      <span v-if="window" class="chart-frame-window">{{ window }}</span>
    </div>

    <p v-if="ranking" class="chart-frame-ranking">{{ ranking }}</p>
    <p v-if="note" class="chart-frame-note">{{ note }}</p>

    <div class="chart-panel-body">
      <slot />
    </div>

    <div v-if="$slots.legend" class="chart-panel-legend">
      <slot name="legend" />
    </div>

    <p v-if="why" class="chart-frame-why">{{ why }}</p>
  </section>
</template>

<style scoped>
.chart-panel {
  background: var(--warm-white);
  border: 1px solid var(--border);
  border-radius: 6px;
  padding: 1.2rem 1.4rem;
  margin-bottom: 1.2rem;
  box-shadow: 0 2px 8px var(--shadow);
  /* Wide bodies (tables, charts) scroll inside the panel rather than pushing the page sideways. */
  overflow-x: auto;
}

.chart-frame-head {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  justify-content: space-between;
  gap: 0.35rem 1rem;
  margin-bottom: 0.55rem;
}

.chart-frame-head h3 {
  font-size: 0.78rem;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  color: var(--ink-light);
  margin-bottom: 0;
}

.chart-frame-window {
  font-size: 0.74rem;
  color: var(--ink-light);
  white-space: nowrap;
}

.chart-frame-ranking {
  font-size: 0.78rem;
  color: var(--ink-faded);
  margin: 0 0 0.65rem;
  line-height: 1.45;
}

/* A rust left rule distinguishes the data caveat from the italic "why this matters" framing: this one says
   "read the data carefully here", not "why this metric matters". */
.chart-frame-note {
  font-size: 0.78rem;
  color: var(--ink-faded);
  margin: 0 0 0.65rem;
  padding-left: 0.6rem;
  border-left: 3px solid var(--rust-light);
  line-height: 1.5;
}

.chart-panel-legend {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem 0.8rem;
  margin-top: 0.75rem;
}

.chart-frame-why {
  font-size: 0.78rem;
  color: var(--ink-faded);
  margin: 0.75rem 0 0;
  line-height: 1.5;
  font-style: italic;
}
</style>
