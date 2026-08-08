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

/**
 * The same list at RUNTIME. [Story 23.2 review 2026-08-07]
 *
 * The union above erases at compile time, so `accent="review"` — a value this component genuinely carried
 * until the 2026-07-28 re-review removed it — emitted `accent-review`, matched no rule, left
 * `--list-row-accent` unset, and rendered as an ordinary row. A "deferred" row silently losing its accent is
 * a wrong signal, not a missing one. `StatusBadge` has guarded exactly this since it was written; the
 * asymmetry was the defect.
 */
const KNOWN_ACCENTS: RowAccent[] = ['done', 'pending', 'deferred', 'ready']

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
 *
 * [Story 23.2 review 2026-08-07] `?? []` covers `null`/`undefined` and nothing else, so a SCALAR slipped
 * through: `:chips="'3 tasks'"` (the shape a loosely-typed IR field takes when it holds one value instead of
 * a list) is truthy, is not nullish, and `v-for` over a string iterates its CHARACTERS — seven single-letter
 * chips. `Array.isArray` is the actual question. Empty strings are dropped in the same pass: `['']` made
 * `chipList.length` truthy, so the `.list-row-meta` cluster rendered solely to hold a blank pill, which is
 * the empty-but-present wrapper NFR8 forbids and this component's header claims it never emits.
 */
const chipList = computed(() => {
  const raw = props.chips
  if (!Array.isArray(raw)) {
    if (import.meta.dev && raw !== null && raw !== undefined) {
      console.warn(
        `[ListRow] \`chips\` must be an array; received ${JSON.stringify(raw)}. Ignoring it — rendering it ` +
          `directly would iterate a string by character. Wrap a single value: :chips="['${String(raw)}']".`,
      )
    }
    return []
  }
  // ⚠️ Coerce rather than drop. The prop's own doc names "a count" as a legitimate chip, so `[3, 'Epic 1']`
  // silently losing the `3` was the documented use case failing quietly — and the dev warning above fires
  // only on the non-array branch, so the per-element drop was invisible even in dev. Numbers and booleans
  // stringify; objects and nullish values are still dropped, because there is no sensible rendering for them.
  // [Story 23.2 review 2026-08-07]
  return raw
    .map((c) => (typeof c === 'string' ? c : typeof c === 'number' || typeof c === 'boolean' ? String(c) : ''))
    .filter((c) => c.trim() !== '')
})

/**
 * `summary` is declared required, but required-ness erases at runtime and `withDefaults` substitutes for
 * `undefined` only, so a JSON IR's `null` reached the template. Rendering the row's own text as an empty
 * span produced a styled row that says nothing. [Story 23.2 review 2026-08-07]
 */
const summaryText = computed(() => (typeof props.summary === 'string' ? props.summary.trim() : ''))

if (import.meta.dev && !summaryText.value) {
  console.warn(
    `[ListRow] missing or empty \`summary\` (received ${JSON.stringify(props.summary)}) — the row's own text ` +
      `is the one thing it cannot do without, so the summary span is omitted rather than rendered blank.`,
  )
}

/**
 * `??` passes `''` through, so `primary-label=""` (an IR field present but empty) rendered an anchor whose
 * entire accessible name was the bare arrow — "link, right arrow" to a screen reader. `||` covers both.
 */
const primaryText = computed(() => props.primaryLabel || 'Open')

/**
 * An out-of-vocabulary accent is dropped rather than emitted as a class that matches nothing, so the row
 * falls back to the neutral default deliberately instead of by accident. [Story 23.2 review 2026-08-07]
 */
const accentClass = computed(() =>
  props.accent && KNOWN_ACCENTS.includes(props.accent) ? `accent-${props.accent}` : null,
)

if (import.meta.dev && props.accent && !KNOWN_ACCENTS.includes(props.accent)) {
  console.warn(
    `[ListRow] unknown accent "${props.accent}" — ignoring it, so this row renders with the neutral rule. ` +
      `Known accents: ${KNOWN_ACCENTS.join(', ')}. Adding one means adding the matching ` +
      `\`.list-row-accent-*\` rule to specscribe.css in the same change.`,
  )
}
</script>

<template>
  <li class="list-row" :class="[accentClass, { resolved }]">
    <div class="list-row-scan">
      <!-- `summary` was the one required prop in this family with neither guard nor dev warning, so
           `:summary="null"` shipped a bordered, padded, accented row with no text in it.
           [Story 23.2 review 2026-08-07] -->
      <span v-if="summaryText" class="list-row-summary">{{ summaryText }}</span>
      <!-- ⚠️ `$slots.badge?.()?.length`, not `$slots.badge`: slot PRESENCE is not slot CONTENT, so
           `<template #badge><StatusBadge v-if="cond"/></template>` with a falsy `cond` rendered an empty
           `.list-row-meta` (`margin-left: auto`) — the empty-but-present wrapper this component's header
           says it never emits (NFR8). [Story 23.2 review 2026-08-07] -->
      <div v-if="$slots.badge?.()?.length || chipList.length || primaryHref" class="list-row-meta">
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

/* The chip carries `list-row-chip pill`, exactly as `ListRow.Chip` (`ListRow.cs`) emits it.
 *
 * ⚠️ EVERY VISUAL PROPERTY IS DELIBERATELY ABSENT HERE. It comes from `.pill` in the generated
 * `assets/shared-primitives.css` — the UNSCOPED shared-primitive layer (ADR 0029), extracted verbatim from
 * `specscribe.css`. This block used to re-declare `.pill`'s ten properties by hand, which drifted exactly as
 * you would expect: serif instead of Courier, no letter-spacing, the wrong padding, and --parchment/--ink-light
 * instead of --warm-white/--ink-faded. The 2026-07-28 re-review fixed the VALUES but could not remove the
 * copy — the scoped `ir-content.css` layer emits `.ir-content .pill`, which reaches injected markup and never
 * a template-authored component, so deleting the properties then shipped an unstyled chip. The shared layer is
 * that missing channel, and this is the copy it deleted.
 *
 * What stays is `flex-shrink`, which is NOT `.pill`'s business: it is this row's layout contract for the meta
 * cluster. A shared primitive describes how a chip LOOKS; where it sits belongs to the component around it.
 * Adding a look property back here re-opens the drift — change `specscribe.css` and re-extract instead. */
.list-row-chip {
  flex-shrink: 0;
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
