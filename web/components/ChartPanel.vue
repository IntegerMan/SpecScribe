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
  <!-- `<div>`, matching `Charts.Framed` (`Charts.cs:168`) exactly. This was a `<section>` with an inner
       `.chart-panel-body` wrapper — neither of which the C# frame emits, and neither of which has any rule in
       specscribe.css. The wrapper was the load-bearing half: it inserted a DOM level the portal has no
       counterpart for, so any `.chart-panel > …` child-combinator rule would apply on the generated portal
       and silently not apply here the moment a surface migrated (23.4) — the same wrapper hazard
       CONVENTIONS §9 already documents for `IrHtml.ts`. It was also unguarded, so `<ChartPanel title="x" />`
       with no children shipped an empty padded div, contradicting this page's own stated contract that an
       unfilled slot renders nothing. [Story 23.2 re-review 2026-07-28] -->
  <div class="chart-panel">
    <div class="chart-frame-head">
      <h3>{{ title }}</h3>
      <span v-if="window" class="chart-frame-window">{{ window }}</span>
    </div>

    <p v-if="ranking" class="chart-frame-ranking">{{ ranking }}</p>
    <p v-if="note" class="chart-frame-note">{{ note }}</p>

    <slot />

    <!-- The one region the C# frame has no concept of. Additive and always guarded, so it cannot change the
         anatomy of a panel that does not use it; `Charts.Framed` renders its legend inside `body`. If a
         legend ever needs to be styled from the shared sheet, add the slot to `Charts.Framed` first. -->
    <div v-if="$slots.legend" class="chart-panel-legend">
      <slot name="legend" />
    </div>

    <p v-if="why" class="chart-frame-why">{{ why }}</p>
  </div>
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
