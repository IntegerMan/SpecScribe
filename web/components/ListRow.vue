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
import { computed } from 'vue'
/**
 * The accent modifiers the shared stylesheet actually defines (`specscribe.css`, `.list-row-accent-*`).
 *
 * Kept in exact correspondence with the portal — the 2026-07-28 re-review found this set had drifted in BOTH
 * directions: it carried a `review` accent the portal has no counterpart for, and had missed `ready` when the
 * portal added it. A fourth accent is a design-system change, not a port; adding one here means adding the
 * matching `.list-row-accent-*` rule to `specscribe.css` in the same change.
 */
export type RowAccent = 'done' | 'pending' | 'deferred' | 'ready'

const props = withDefaults(
  defineProps<{
    /** The row's primary text. */
    summary: string
    /** Optional left-rule accent token. Reinforcement only, never the meaning. */
    accent?: RowAccent
    /** Small secondary facts (a date, a count, a source). */
    chips?: string[] | null
    /** The row's ONE primary affordance. Rendered arrow-suffixed so it reads the same everywhere. */
    primaryHref?: string
    primaryLabel?: string
    /** Resolved rows de-emphasise and take the done accent. */
    resolved?: boolean
  }>(),
  { accent: undefined, chips: () => [], primaryHref: undefined, primaryLabel: undefined, resolved: false },
)

/**
 * `withDefaults` substitutes for `undefined` ONLY. A JSON IR emits `null` for an absent array, and
 * `chips.length` on `null` threw during render — failing the WHOLE route's SSR, not just the row. 23.3 feeds
 * these components from the IR, so null-vs-undefined is a live input class, not a hypothetical.
 */
const chipList = computed(() => props.chips ?? [])

/**
 * `??` passes `''` through, so `primary-label=""` (an IR field present but empty) rendered an anchor whose
 * entire accessible name was the bare arrow — "link, right arrow" to a screen reader. `||` covers both.
 */
const primaryText = computed(() => props.primaryLabel || 'Open')
</script>

<template>
  <li class="list-row" :class="[accent ? `accent-${accent}` : null, { resolved }]">
    <div class="list-row-scan">
      <span class="list-row-summary">{{ summary }}</span>
      <div v-if="$slots.badge || chipList.length || primaryHref" class="list-row-meta">
        <slot name="badge" />
        <!-- Keyed by INDEX, not by text: `chips` is a plain `string[]` with no uniqueness constraint, and two
             identical facts (`['3 tasks', '3 tasks']`) produced duplicate keys and node reuse on reorder. -->
        <span v-for="(chip, i) in chipList" :key="i" class="list-row-chip pill">{{ chip }}</span>
        <a v-if="primaryHref" class="list-row-primary" :href="primaryHref">
          {{ primaryText }} &rarr;
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

/* `ready`, not `review`: these four mirror `.list-row-accent-*` in specscribe.css exactly. */
.accent-ready {
  --list-row-accent: var(--status-ready);
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

/* The chip carries `list-row-chip pill`, exactly as `ListRow.Chip` (`ListRow.cs:73`) emits it, and every
   property below is `.pill`'s (`specscribe.css:1234`) — Courier, 0.03em tracking, 0.2rem/0.7rem, the 999px
   radius, --warm-white, --ink-faded. Before the 2026-07-28 re-review this rule declared serif, no tracking,
   0.1rem/0.55rem and --parchment/--ink-light, so the Vue chip and the portal chip were visibly different
   objects inside a file whose header calls itself "the Vue counterpart of ListRow.Render".

   ⚠️ THIS IS STILL A SECOND COPY, and knowingly so. `.pill` is shared vocabulary that lives in the C#
   monolith; the Vue app imports only `tokens.css`, `base.css` and the GENERATED `ir-content.css`, and the
   latter scopes it as `.ir-content .pill` — so it reaches IR-injected markup and never reaches a
   template-authored component. Dropping these properties in favour of the `pill` class alone ships an
   UNSTYLED chip. Closing this properly needs a channel for shared non-IR primitive classes, which does not
   exist yet; it is raised as a decision on Story 23.2's re-review. The `pill` class is kept on the element
   so that the day such a channel lands, this block deletes cleanly. */
.list-row-chip {
  flex-shrink: 0;
  font-family: 'Courier New', monospace;
  font-size: 0.72rem;
  letter-spacing: 0.03em;
  padding: 0.2rem 0.7rem;
  border-radius: 999px;
  border: 1px solid var(--border);
  background: var(--warm-white);
  color: var(--ink-faded);
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
