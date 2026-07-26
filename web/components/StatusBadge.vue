<script setup lang="ts">
/**
 * The six-stage lifecycle badge — the Vue counterpart of `StatusStyles.Badge` / `.status-badge` in
 * specscribe.css.
 *
 * UX-DR17 is enforced BY THE COMPONENT'S SHAPE, not by discipline at each call site: `label` is a required
 * prop, so a badge cannot be rendered as colour alone. The stage class only ever adds reinforcement.
 *
 * Note what is NOT here: no stage -> word map and no stage -> meaning map. Those belong to the data
 * (C# `StatusStyles` today, the canonical IR from 23.3 on). A parallel copy in JS would be a second status
 * vocabulary free to drift from the one the portal renders — exactly the class of drift Epic 23 exists to
 * end. This component styles a status; it does not know what statuses exist.
 */

/** Canonical stage tokens — the `.status-badge.<stage>` modifiers the shared stylesheet defines. */
export type StatusStage =
  | 'pending'
  | 'drafted'
  | 'ready'
  | 'active'
  | 'review'
  | 'done'
  | 'deferred'
  | 'retired'
  | 'unrecognized'

withDefaults(
  defineProps<{
    /** Lifecycle stage token. Drives colour only — never the meaning. */
    stage: StatusStage
    /** The status WORD. Required: colour is never the sole signal (UX-DR17). */
    label: string
    /** One-line plain-language meaning, surfaced as the native tooltip (works with JS off). */
    meaning?: string
  }>(),
  { meaning: undefined },
)
</script>

<template>
  <span class="status-badge" :class="`is-${stage}`" :title="meaning">{{ label }}</span>
</template>

<style scoped>
/* Scoped: Vue rewrites every selector below to `[data-v-*]`, so these rules cannot leak into any other
   component or into injected content. That containment is the whole point of the migration — a single
   global sheet gave none of it (see the star-slash comment-truncation incident that silently killed ~1,000
   rules; the offending sequence is deliberately not spelled out here, because writing it inside a CSS
   comment is the bug). */
.status-badge {
  display: inline-block;
  font-size: 0.66rem;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  padding: 0.15rem 0.6rem;
  border-radius: 3px;
  font-family: 'Courier New', Courier, monospace;
  white-space: nowrap;
  /* Neutral base = the pending/deferred reading; each stage below overrides it. */
  background: var(--parchment-dark);
  color: var(--ink-light);
  border: 1px solid var(--border);
}

/* Every value is a token reference. No stage re-types a colour. */
.is-done {
  background: var(--parchment);
  color: var(--moss);
  border-color: var(--status-done);
}

.is-active {
  background: var(--parchment);
  color: var(--teal);
  border-color: var(--status-active);
}

.is-review {
  background: var(--parchment);
  color: var(--teal-deep);
  border-color: var(--status-review);
}

.is-ready,
.is-drafted {
  background: var(--parchment);
  color: var(--gold);
  border-color: var(--status-ready);
}

.is-pending {
  background: var(--parchment-dark);
  color: var(--ink-light);
  border-color: var(--status-pending);
}

.is-deferred {
  background: var(--parchment-dark);
  color: var(--ink-light);
  border-color: var(--status-deferred);
}

.is-retired {
  background: var(--parchment-dark);
  color: var(--ink-light);
  border-color: var(--border);
}

/* Present-but-unmapped native status: hatched + dashed, so it reads as "outside the vocabulary" by TEXTURE
   as well as by word — never as one of the six real stages. */
.is-unrecognized {
  background: repeating-linear-gradient(
    -45deg,
    var(--warm-white),
    var(--warm-white) 3px,
    var(--status-unrecognized-hatch) 3px,
    var(--status-unrecognized-hatch) 6px
  );
  color: var(--status-unrecognized);
  border: 1px dashed var(--status-unrecognized);
}
</style>
