<script setup lang="ts">
/**
 * The framed chart panel — the Vue counterpart of `Charts.Framed(ChartMeta, …)` plus `.chart-panel` /
 * `.chart-frame-*` in specscribe.css (Story 10.2; the `note` slot is Story 10.6).
 *
 * The point of the C# helper is that title / window / ranking / note / why are metadata-consistent BY
 * CONSTRUCTION — no call site hand-writes the frame. Props here carry the same contract, in the same
 * render order, so a panel authored in Vue carries the same anatomy as one authored in C# for every region
 * the C# frame has:
 *
 *   head (title + window) -> ranking -> note -> body -> why
 *
 * ⚠️ With ONE enumerated exception, stated here rather than left to be discovered in the template: `legend`.
 * `Charts.Framed` has no legend slot and renders a legend inside `body`. Ours is additive and guarded on
 * rendered content, so it cannot change the anatomy of a panel that does not fill it — but a panel that DOES
 * fill it has a region the C# frame does not emit, and the blanket claim this comment used to make ("cannot
 * grow a different anatomy") was therefore already untrue when it was written. Keeping the slot was the
 * owner's call on 2026-07-28; narrowing the sentence to match is Story 23.2's fourth review pass. If a legend
 * ever needs styling from the shared sheet, add the slot to `Charts.Framed` first so both frames agree.
 *
 * `legend` is a named slot rather than a prop because a legend is markup, not metadata.
 */
const props = defineProps<{
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

if (import.meta.dev && (typeof props.title !== 'string' || props.title.trim() === '')) {
  // The ONE field on this component with no empty guard, on the component whose own documentation states
  // "a slot with nothing to say renders nothing at all, rather than an empty heading". Every optional region
  // is `v-if`-guarded; the REQUIRED one was not, so `title=""` or `:title="null"` (an IR field present but
  // empty) shipped an empty `<h3>` — an unlabelled heading inside a landmark-bearing panel, which is worse
  // for a screen-reader user than no heading at all. [Story 23.2 review 2026-08-07]
  console.warn(
    `[ChartPanel] missing or empty title (received ${JSON.stringify(props.title)}). This renders an empty ` +
      `<h3> inside the panel — an unlabelled heading. Every panel needs a name.`,
  )
}
</script>

<template>
  <!-- `<div>`, matching `Charts.Framed` (`Charts.Framed` in `Charts.cs` — by SYMBOL, not by line: this said
       `Charts.cs:168` and had already drifted six lines by the time anyone checked, which is precisely what
       CLAUDE.md means by "confirm by symbol"). This was a `<section>` with an inner
       `.chart-panel-body` wrapper — neither of which the C# frame emits, and neither of which has any rule in
       specscribe.css. The wrapper was the load-bearing half: it inserted a DOM level the portal has no
       counterpart for, so any `.chart-panel > …` child-combinator rule would apply on the generated portal
       and silently not apply here the moment a surface migrated (23.4) — the same wrapper hazard
       CONVENTIONS §9 already documents for `IrHtml.ts`. It was also unguarded, so `<ChartPanel title="x" />`
       with no children shipped an empty padded div, contradicting this page's own stated contract that an
       unfilled slot renders nothing. [Story 23.2 re-review 2026-07-28] -->
  <div class="chart-panel">
    <div class="chart-frame-head">
      <!-- `title` is required, but required-ness erases at runtime and covers `undefined` only — `:title="null"`
           or `title=""` rendered an empty <h3>: an unlabelled heading inside a landmark-bearing panel, and the
           one field in this frame with no guard while every other region is `v-if`ed.
           [Story 23.2 review 2026-08-07] -->
      <h3 v-if="title">{{ title }}</h3>
      <span v-if="window" class="chart-frame-window">{{ window }}</span>
    </div>

    <p v-if="ranking" class="chart-frame-ranking">{{ ranking }}</p>
    <p v-if="note" class="chart-frame-note">{{ note }}</p>

    <slot />

    <!-- The one region the C# frame has no concept of. Additive and always guarded, so it cannot change the
         anatomy of a panel that does not use it; `Charts.Framed` renders its legend inside `body`. If a
         legend ever needs to be styled from the shared sheet, add the slot to `Charts.Framed` first.

         ⚠️ `$slots.legend?.()?.length`, not `$slots.legend`: the latter is truthy whenever the slot was
         PASSED, regardless of whether it rendered anything, so `<template #legend><X v-if="cond"/></template>`
         with a falsy `cond` emitted an empty `.chart-panel-legend` carrying `margin-top: 0.75rem` — the
         empty-but-present wrapper this file's contract forbids. [Story 23.2 review 2026-08-07] -->
    <div v-if="$slots.legend?.()?.length" class="chart-panel-legend">
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
