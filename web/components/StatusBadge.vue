<script setup lang="ts">
/**
 * The six-stage lifecycle badge — the Vue counterpart of `StatusStyles.Badge` / `.status-badge` in
 * specscribe.css.
 *
 * UX-DR17 — never signal by colour alone — is served here by the required `label` prop: the word is always
 * present, so the badge is legible in greyscale and to a screen reader.
 *
 * ⚠️ WHAT THIS COMPONENT DOES NOT YET DO, stated plainly because an earlier version of this header claimed
 * otherwise. The portal's `StatusStyles.Badge` emits `icon + word` and documents the rule as "color + icon +
 * word, never icon-only". **This component renders no icon.** That matters for two pairs the portal separates
 * by glyph alone: `ready`/`drafted` share a border colour, and `deferred`/`retired` are byte-identical rule
 * sets here. Supplying the glyph is a **Story 23.3 dependency** — the stage -> icon mapping needs a data
 * source, and the canonical IR is it (see CONVENTIONS.md §5). Until then the word carries the whole load.
 *
 * Note what is also NOT here: no stage -> word map and no stage -> meaning map. Those belong to the data
 * (C# `StatusStyles` today, the canonical IR from 23.3 on). A parallel copy in JS would be a second status
 * vocabulary free to drift from the one the portal renders — exactly the class of drift Epic 23 exists to
 * end. This component styles a status; it does not know what statuses exist.
 */
import { computed } from 'vue'

/**
 * Canonical stage tokens — the `.status-badge.<stage>` modifiers the shared stylesheet defines.
 *
 * All TEN of `StatusStyles.LegendStages`. `unmapped` was missing until the 2026-07-28 re-review, which left
 * 23.3 no legal way to render a requirement that is listed but mapped to no epic: the only substitute was
 * `stage="pending"`, collapsing exactly the distinction `StatusStyles.Badge`'s three-arg overload exists to
 * preserve. Like the portal, `unmapped` borrows the pending COLOUR and stays distinct by word.
 */
export type StatusStage =
  | 'pending'
  | 'drafted'
  | 'ready'
  | 'active'
  | 'review'
  | 'done'
  | 'deferred'
  | 'unmapped'
  | 'retired'
  | 'unrecognized'

const KNOWN_STAGES: readonly StatusStage[] = [
  'pending',
  'drafted',
  'ready',
  'active',
  'review',
  'done',
  'deferred',
  'unmapped',
  'retired',
  'unrecognized',
]

const props = withDefaults(
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

/**
 * TypeScript unions erase at runtime, so a stage string from the IR can be anything. An unknown value used to
 * match no `.is-*` rule and fall through to the base rule — which this file's own comment describes as "the
 * pending/deferred reading", i.e. an out-of-vocabulary status silently displayed as if it were Pending.
 * `unrecognized` is the stage that MEANS "no canonical mapping", and it is hatched and dashed, so an unknown
 * value now reads as outside the vocabulary by texture rather than impersonating a real stage.
 */
const stageClass = computed(() =>
  KNOWN_STAGES.includes(props.stage) ? `is-${props.stage}` : 'is-unrecognized',
)

if (import.meta.dev) {
  if (!KNOWN_STAGES.includes(props.stage)) {
    console.warn(
      `[StatusBadge] unknown stage "${props.stage}" — rendering as "unrecognized". ` +
        `Known stages: ${KNOWN_STAGES.join(', ')}.`,
    )
  }
  // Required-ness guards `undefined`, not `''`. An empty label is a colour-only badge, which is the one thing
  // this component must never render — so it is a loud dev failure rather than a silent UX-DR17 breach.
  if (props.label.trim() === '') {
    console.warn(
      `[StatusBadge] empty label for stage "${props.stage}" — this renders a colour-only badge and breaks ` +
        `UX-DR17. Pass the status WORD (StatusStyles.LegendWord on the C# side).`,
    )
  }
}
</script>

<template>
  <span class="status-badge" :class="stageClass" :title="meaning">{{ label }}</span>
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

/* `unmapped` deliberately shares pending's COLOUR — it is a requirement-level state, not an eleventh
   lifecycle stage — and stays distinct by word ("Not yet mapped"). Mirrors the portal's own remap in
   `StatusStyles.LegendKey` / `RequirementBadge`, where the colour is shared and the glyph is not. */
.is-pending,
.is-unmapped {
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
