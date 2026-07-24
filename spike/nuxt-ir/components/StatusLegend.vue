<script setup lang="ts">
// The token-driven scoped-CSS probe (AC #2 sub-risk 1). Every colour below is read from the SHIPPED
// stylesheet's custom properties — no value is re-typed here, so this component cannot drift from the
// six --status-* tokens (AD-7 / the status-token system).
const stages = ['done', 'review', 'active', 'ready', 'drafted', 'deferred'] as const
</script>

<template>
  <div class="legend">
    <span class="legend-title">Scoped-SFC token probe</span>
    <span v-for="stage in stages" :key="stage" class="swatch" :class="`is-${stage}`">{{ stage }}</span>
  </div>
</template>

<style scoped>
/* Plain `scoped` — Vue rewrites these to [data-v-*] attribute selectors and they apply only to this SFC's own
   markup. That containment is the point: this is the failure class the CSS-comment truncation incident showed
   a single global stylesheet cannot give you. */
.legend {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
  align-items: center;
  margin: 0 0 1rem;
  padding: 0.6rem 0.8rem;
  border: 1px dashed var(--border, #ccc);
  border-radius: 0.4rem;
  font-size: 0.8rem;
}

.legend-title {
  font-weight: 600;
  opacity: 0.75;
}

.swatch {
  padding: 0.15rem 0.5rem;
  border-radius: 0.25rem;
  color: #fff;
  transition: transform var(--motion-fast, 150ms) ease;
}

.swatch:hover {
  transform: translateY(-1px);
}

.is-done { background: var(--status-done); }
.is-review { background: var(--status-review); }
.is-active { background: var(--status-active); }
.is-ready { background: var(--status-ready); }
.is-drafted { background: var(--status-drafted); color: #2b2b2b; }
.is-deferred { background: var(--status-deferred); }
</style>
