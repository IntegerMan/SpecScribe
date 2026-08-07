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
 * word, never icon-only". **This component renders no icon.** That matters for `ready`/`drafted`, which share
 * a border colour and which the portal separates by glyph alone.
 *
 * [Story 23.2 review 2026-08-07] This header used to add that `deferred`/`retired` "are byte-identical rule
 * sets here". They are not: `.is-deferred` binds `var(--status-deferred)` and `.is-retired` binds
 * `var(--border)`, so they differ by border as well as by word. The claim was inherited from an earlier state
 * of this file and was one of the stated justifications for deferring the glyph, so it is corrected rather
 * than dropped.
 *
 * The glyph deferral to Story 23.3 is **withdrawn**: the IR-backed surfaces never instantiate this component
 * — they inject C#-rendered markup that already carries the glyph — so 23.3 was never in a position to
 * discharge it. Since the `/design-system` showcase was retired, this component's only remaining callers are
 * the `/measure/*` payload fixtures. Treat it as fixture-grade, not as a shipped primitive, and read
 * CONVENTIONS.md §5 before building a product surface on it.
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
  // Required-ness guards `undefined`, not `''` — and not `null`, which is what a JSON IR emits for an absent
  // field and what `label: string` cannot prevent at runtime. Any of the three is a colour-only badge, the
  // one thing this component must never render, so it is a loud dev failure rather than a silent UX-DR17
  // breach. Note the ORDER: `props.label.trim()` was called unguarded, so a `null` label threw a TypeError
  // and failed the whole route's SSR in dev, while in production the dev block is compiled out and the same
  // input shipped the colour-only badge silently. [Story 23.2 review 2026-08-07]
  if (typeof props.label !== 'string' || props.label.trim() === '') {
    console.warn(
      `[StatusBadge] missing or empty label for stage "${props.stage}" (received ${JSON.stringify(props.label)}) ` +
        `— this renders a colour-only badge and breaks UX-DR17. Pass the status WORD ` +
        `(StatusStyles.LegendWord on the C# side).`,
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

/* Every value is a token reference. No stage re-types a colour.
 *
 * The four FILLS below used to be `var(--parchment)` for want of a token: the portal's own
 * `.status-badge.<stage>` rules carried inline hexes (`#e8f0e4`, `#e0ecea`, `#d9e6ea`, `#f5ecd4`), and the
 * bridge structurally CANNOT carry an untokenized literal — so this component substituted one flat parchment
 * for four distinct tints and the two design systems rendered visibly different badges. The 2026-07-28
 * re-review raised it; the owner's fix was to tokenize the source rather than accept the divergence, so
 * `--status-*-bg` now exists in `specscribe.css` and crosses the bridge like every other token. */
.is-done {
  background: var(--status-done-bg);
  color: var(--moss);
  border-color: var(--status-done);
}

.is-active {
  background: var(--status-active-bg);
  color: var(--teal);
  border-color: var(--status-active);
}

.is-review {
  background: var(--status-review-bg);
  color: var(--teal-deep);
  border-color: var(--status-review);
}

/* One fill for both, exactly as the portal pairs them — separated by word and glyph, not by tint. */
.is-ready,
.is-drafted {
  background: var(--status-ready-bg);
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
