<script setup lang="ts">
/**
 * The unified list row — the Vue counterpart of `ListRow.Render` and the `.list-row*` grammar in
 * specscribe.css (Story 10.8).
 *
 * Anatomy, in the same order the C# primitive emits it:
 *   scan line -> summary | meta cluster (badge slot, chips, one primary link)
 *
 * The `accent` prop drives the left rule ONLY. It is never the sole signal — the row's badge still carries
 * the word (Story 10.8 review; UX-DR17). Rows with no lifecycle meaning at all (an ADR list, a timeline)
 * simply omit it and get the neutral border.
 *
 * Like StatusBadge, this renders the meta cluster only when something fills it, so a bare row never carries
 * an empty-but-present wrapper (NFR8).
 */
export type RowAccent = 'done' | 'pending' | 'deferred' | 'review'

withDefaults(
  defineProps<{
    /** The row's primary text. */
    summary: string
    /** Optional left-rule accent token. Reinforcement only, never the meaning. */
    accent?: RowAccent
    /** Small secondary facts (a date, a count, a source). */
    chips?: string[]
    /** The row's ONE primary affordance. Rendered arrow-suffixed so it reads the same everywhere. */
    primaryHref?: string
    primaryLabel?: string
    /** Resolved rows de-emphasise and take the done accent. */
    resolved?: boolean
  }>(),
  { accent: undefined, chips: () => [], primaryHref: undefined, primaryLabel: undefined, resolved: false },
)
</script>

<template>
  <li class="list-row" :class="[accent ? `accent-${accent}` : null, { resolved }]">
    <div class="list-row-scan">
      <span class="list-row-summary">{{ summary }}</span>
      <div v-if="$slots.badge || chips.length || primaryHref" class="list-row-meta">
        <slot name="badge" />
        <span v-for="chip in chips" :key="chip" class="list-row-chip">{{ chip }}</span>
        <a v-if="primaryHref" class="list-row-primary" :href="primaryHref">
          {{ primaryLabel ?? 'Open' }} &rarr;
        </a>
      </div>
    </div>
  </li>
</template>

<style scoped>
.list-row {
  list-style: none;
  background: var(--warm-white);
  border: 1px solid var(--border);
  border-radius: 6px;
  padding: 0.7rem 1rem;
  margin-bottom: 0.55rem;
  /* Neutral by default; an accent modifier below re-points the same custom property. One rule, not five. */
  border-left: 3px solid var(--list-row-accent, var(--border));
}

.accent-done {
  --list-row-accent: var(--status-done);
}

.accent-pending {
  --list-row-accent: var(--status-pending);
}

.accent-deferred {
  --list-row-accent: var(--status-deferred);
}

.accent-review {
  --list-row-accent: var(--status-review);
}

.resolved {
  --list-row-accent: var(--status-done);
  opacity: 0.78;
}

.list-row-scan {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 0.45rem 0.85rem;
  width: 100%;
}

.list-row-summary {
  flex: 1 1 16rem;
  min-width: 0;
  color: var(--ink);
  font-size: 0.92rem;
  line-height: 1.45;
  text-wrap: pretty;
}

.list-row-meta {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.4rem 0.55rem;
  flex: 0 1 auto;
  margin-left: auto;
}

.list-row-chip {
  flex-shrink: 0;
  font-size: 0.72rem;
  color: var(--ink-light);
  background: var(--parchment);
  border: 1px solid var(--border);
  border-radius: 999px;
  padding: 0.1rem 0.55rem;
}

.list-row-primary {
  flex-shrink: 0;
  font-size: 0.78rem;
  color: var(--teal);
  text-decoration: none;
  border-bottom: 1px dotted var(--gold-light);
  white-space: nowrap;
}

a.list-row-primary:hover {
  color: var(--rust);
}
</style>
