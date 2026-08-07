// Shared extraction logic for the IR-content stylesheet layer (Story 23.3, AC #6).
//
// The problem this solves. The Nuxt app imports ONLY `tokens.css` from the C# side (23.2 owner decision 2),
// but the IR's `contentHtml` is markup authored against the full 7,041-line `specscribe.css`. Without a
// second layer, every migrated page renders structurally correct and visually bare — the 23.1 spike hid
// this by importing the monolith wholesale, which is the shape 23.2 deliberately walked away from.
//
// The trade. This IS monolith-derived CSS, and pretending otherwise would be dishonest. What makes it a
// transitional layer rather than a re-import is that it is:
//
//   1. BOUNDED  — only rules whose selectors are actually used by the four migrated families' markup;
//   2. GENERATED — never hand-authored, and gated in both directions by `check:ir-content`;
//   3. SCOPED   — every rule is emitted under `.ir-content`, so it cannot reach a template-authored
//                 component even by accident;
//   4. ENUMERATED — `assets/ir-content.manifest.json` names every source rule carried, by SELECTOR (plus
//                 the at-rule it sits within). That list is the surface Story 23.4 has to retire; implied
//                 debt is debt nobody pays. It records no line spans on purpose — see the committed-fields
//                 rule in `ir-content-build.mjs`, and grep the selector instead.
//
// ⚠️ Never write the `*` + `/` sequence inside a CSS comment in a generated or hand-authored sheet here.
// That exact mistake silently closed a comment in `specscribe.css` and took ~1,000 rules with it, invisible
// to the whole test suite. This module strips source comments rather than carrying them, which removes the
// hazard by construction; the generated banner below is the only comment in the output.

import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

export const SOURCE_CSS = fileURLToPath(
  new URL('../../src/SpecScribe/assets/specscribe.css', import.meta.url),
)
export const OUT_CSS = fileURLToPath(new URL('../assets/ir-content.css', import.meta.url))
export const OUT_MANIFEST = fileURLToPath(new URL('../assets/ir-content.manifest.json', import.meta.url))
export const OUT_SHARED_CSS = fileURLToPath(new URL('../assets/shared-primitives.css', import.meta.url))
export const OUT_RUNTIME_CSS = fileURLToPath(new URL('../assets/runtime-body.css', import.meta.url))

/** Repo-relative label — never an absolute machine path, which would differ per checkout. */
export const SOURCE_LABEL = 'src/SpecScribe/assets/specscribe.css'

/** The scope every emitted rule is nested under. Falls through onto `PageShell`'s root element. */
export const SCOPE = '.ir-content'

// ── The shared-primitive layer (UNSCOPED) ────────────────────────────────────────────────────────────────
//
// ⚠️ This layer deliberately breaks property 3 (SCOPED) above, for a bounded allowlist. See ADR 0029, which
// amends ADR 0018 to permit exactly this and no more.
//
// WHY IT EXISTS. `.pill` is SHARED VOCABULARY: `ListRow.Chip` (`ListRow.cs`) emits
// `class="list-row-chip pill"`, and every visual property of that chip comes from `.pill` in the C# monolith.
// `ListRow.vue` is a TEMPLATE-AUTHORED component, so the scoped layer cannot reach it — `.ir-content .pill`
// only ever matches injected markup. That left exactly two options, and for a while the code took the wrong
// one: hand-retype `.pill`'s declarations inside the SFC. Story 23.2's re-review found the copy had drifted
// (serif instead of Courier, wrong padding, wrong tokens), fixed the VALUES, and recorded that it was still a
// second definition with no channel to remove it. This is that channel.
//
// WHAT KEEPS IT HONEST. The other three ADR 0018 properties are strengthened rather than relaxed:
//
//   BOUNDED    — an explicit allowlist, not a usage harvest. Nothing enters it by accident, and a rule is
//                carried only when EVERY class it names is on the list, so `.pill.status-complete` stays in
//                the scoped layer where it belongs (those ADR-status variants are IR content's business).
//   GENERATED  — same source, same builder, same both-directions gate as the scoped layer.
//   ENUMERATED — the manifest's `sharedPrimitives` block names every rule that moved, so Story 23.4 sees
//                this layer in the same list it already has to retire. Implied debt is debt nobody pays.
//
// EXACTLY ONE DEFINITION. A selector that lands here is REMOVED from the scoped layer rather than duplicated
// into both. An unscoped `.pill` still matches inside `.ir-content`, so injected markup keeps its styling,
// and the app now has one place `.pill` is defined instead of two. The manifest records the handoff so the
// rule does not simply vanish from the scoped layer's list.
//
// ADDING TO THIS LIST IS AN ARCHITECTURAL DECISION, not a convenience. The test that a candidate must pass:
// is this class emitted by a C# primitive AND consumed by a template-authored Vue component? If it is only
// ever in injected markup, the scoped layer already covers it and this list must not grow.

/**
 * Class names whose rules are emitted UNSCOPED, so template-authored components can use them.
 *
 * One entry today. `list-row-chip` is deliberately NOT here: it is the Vue component's own class and belongs
 * in its `<style scoped>` block — only the SHARED half (`pill`) crosses.
 */
export const SHARED_PRIMITIVES = ['pill']

const SHARED_SET = new Set(SHARED_PRIMITIVES)

// ── The runtime-body layer (UNSCOPED) ────────────────────────────────────────────────────────────────────
//
// ⚠️ A SECOND deliberate break of property 3 (SCOPED), for a second bounded allowlist, and NOT a widening of
// the shared-primitive list above. See ADR 0039, which amends ADR 0029 to permit exactly this and no more.
//
// WHY IT IS A DIFFERENT CATEGORY. `SHARED_PRIMITIVES` answers "a C# primitive emits it AND a
// template-authored Vue component consumes it" — both ends are markup. These classes have no markup end at
// all on the page: `specscribe.js` creates the node at RUNTIME and attaches it to `document.body`, which is
// OUTSIDE the `.ir-content` wrapper every scoped rule is nested under. `.ir-content .ss-tooltip` can
// therefore never match, no matter what the harvest saw. Scoping is not merely unhelpful here, it is wrong.
//
// The body-level placement is deliberate and load-bearing, so "move the node instead" is not the cheaper
// fix it looks like: `.ss-tooltip` is `position: absolute; z-index: 300` precisely so it layers above the
// sticky nav and is clamped to the viewport rather than clipped by whatever ancestor it would otherwise sit
// in (specscribe.css § "shared body-level `.ss-tooltip`"), and `specscribe.js` computes its coordinates in
// PAGE space by adding `scrollX`/`scrollY`. Re-parenting it under `.ir-content` would trade a styling bug
// for a positioning-and-clipping bug. [ADR 0039]
//
// ⚠️ HARVEST VISIBILITY IS NOT THE TEST — CONTAINMENT IS. `codemap-card*` is the worked example, and it is
// why this list is not simply "whatever the harvest misses". Its markup IS server-built (`Charts.cs`
// `BuildTreemapCard` writes `<div class='codemap-card'>` into the `data-tip-html` attribute), so the
// harvest finds it and its rules were carried into the SCOPED layer perfectly happily — and were dead on
// the page anyway, because that markup is only ever `innerHTML`'d into the body-level `.ss-tooltip` node.
// A class can be perfectly visible to the harvest and still need to be here.
//
// THE ADMISSION TEST, so this list cannot grow by association: is this class only ever applied to a node
// that is provably OUTSIDE `.ir-content`? "It is styled by JS-generated markup" is not sufficient — the
// hierarchy explorer's own sector, probe, legend-swatch and breadcrumb classes are all runtime-applied too,
// and they live INSIDE the chart panel, so they belong in the scoped layer and are seeded through
// `CONDITIONAL_CLASSES` instead.

/**
 * Class names whose rules are emitted UNSCOPED because the node carrying them lives outside `.ir-content`.
 *
 * Two families, both of them tooltip: the shared body-level tip node itself, and the two rich cards that are
 * rendered into it as `innerHTML` — the hierarchy explorer's (built in `specscribe.js` `tipCardFor`) and the
 * code map's (built in C# `Charts.BuildTreemapCard`).
 */
export const RUNTIME_BODY_CLASSES = [
  'ss-tooltip', //               the shared tip node — `ensureTip()` appends it to document.body
  // The hierarchy explorer's rich card, built by `specscribe.js` `tipCardFor()` and set as the tip's
  // innerHTML. Never in any markup, so the harvest cannot see it AND it could not be scoped if it could.
  'ss-hierarchy-card',
  'ss-hierarchy-card-kind',
  'ss-hierarchy-card-name',
  'ss-hierarchy-card-status',
  'ss-hierarchy-card-detail',
  'ss-hierarchy-card-hint',
  // The code map's card. Server-built into `data-tip-html`, therefore HARVESTED — these rules were being
  // carried into the scoped layer and were inert there. See the containment note above.
  'codemap-card',
  'codemap-card-name',
  'codemap-card-path',
  'codemap-card-metrics', // its dt/dd rows are element selectors under this class, so they ride along
]

const RUNTIME_SET = new Set(RUNTIME_BODY_CLASSES)

/**
 * Does this selector belong to the unscoped shared layer?
 *
 * Requires that it names at least one class, that EVERY class it names is on the allowlist, and that it names
 * no id. The all-or-nothing rule is what keeps the layer from growing by association: `.pill.status-draft`
 * names a class that is not shared vocabulary, so it stays scoped even though `.pill` is on the list.
 */
export function isSharedPrimitive(selector) {
  const normalized = selector.replace(/:root\b/g, 'html')
  const { classes, ids } = selectorTokens(normalized)
  if (classes.length === 0 || ids.length > 0) return false
  return classes.every((c) => SHARED_SET.has(c))
}

/**
 * Does this selector belong to the unscoped runtime-body layer? [ADR 0039]
 *
 * Same all-or-nothing shape as `isSharedPrimitive`, and for the same reason: a rule is carried only when
 * EVERY class it names is on the allowlist, so a selector that reaches from the tip node back into page
 * content (were one ever written) stays scoped rather than silently escaping containment.
 *
 * The two allowlists are disjoint by construction — see the guard test in `test/ir-content-lib.test.mjs`.
 * A selector can therefore never be claimed by both layers, so "exactly one definition" still holds.
 */
export function isRuntimeBodyClass(selector) {
  const normalized = selector.replace(/:root\b/g, 'html')
  const { classes, ids } = selectorTokens(normalized)
  if (classes.length === 0 || ids.length > 0) return false
  return classes.every((c) => RUNTIME_SET.has(c))
}

/**
 * The families whose markup drives the extraction.
 *
 * ⚠️ **Story 23.4 widened this from four families to the WHOLE SITE, and the reason is not tidiness.**
 *
 * Story 23.3 bounded it to four families deliberately — "driving usage off all 1,042 pages would pull in most
 * of the monolith and turn a bounded layer back into a wholesale import" — and reported the shortfall for
 * everything else as a coverage number, because those pages were `PassThroughSurface` and explicitly not
 * claimed as migrated.
 *
 * Once Story 23.4 migrated the remaining **1,276** pages, that bound stopped being conservative and became
 * simply WRONG: the extractor was reporting 42 % class coverage for pages the router now renders as real
 * families, which means ~58 % of the classes those pages emit had **no rule at all**. Nothing fails, nothing
 * is logged — the element just renders bare. That is exactly ADR 0018's rejected alternative #3 ("ship
 * unstyled and fix it in 23.4"), arrived at by omission rather than by decision.
 *
 * **What widening does and does not give up.** It does give up the "62 % smaller than source" figure — with
 * every family in scope the layer converges toward a scoped mirror of the monolith, and that is reported
 * honestly rather than buried. It does NOT give up the two properties ADR 0018 actually rests on:
 *
 *   · **Containment.** Every rule is still re-nested under `.ir-content`, so a monolith rule still cannot
 *     reach a template-authored component. That was always the blast-radius argument, not the rule count.
 *   · **Generated + gated both ways.** `npm run check:ir-content` still re-derives and still fails on a
 *     hand-edit or an un-re-extracted source change. No rule is hand-copied, so ADR 0018's rejected
 *     alternative #2 ("a second definition free to drift") is still avoided.
 *
 * A usage-driven extractor is *supposed* to track its usage set. The set grew; the layer grows with it. What
 * this makes undeniable is the size of the Epic 22 ask — see ADR 0018 §Addendum and
 * `npm run report:ir-content-residue`.
 */
export const MIGRATED = {
  wholeSite: () => true,
}

/**
 * Every IR page drives the extraction now. Kept as a named predicate (rather than deleting the call sites)
 * because it is the one place the bound is decided, and a future story narrowing it again should have to
 * change this function and read the note above.
 */
export const isMigrated = () => true

// ── Data-conditional classes ─────────────────────────────────────────────────────────────────────────────
//
// ⚠️ The extraction below asks "does this class appear in the markup the migrated families render RIGHT
// NOW?". For most selectors that is the right question. For a class whose presence is a function of
// PROJECT DATA rather than of the templates, it is the wrong one, and it fails in both directions:
//
//   1. FALSE DRIFT. `check:ir-content` re-derives from the live IR, so moving an epic into review or
//      filling the last empty sprint lane reddens the gate on a commit that touched neither the stylesheet
//      nor a template. Observed 2026-07-28: `.epic-remaining-review` appeared and `.sprint-lane-empty`
//      vanished in the same run, from sprint work alone.
//   2. SILENT STYLE LOSS — the worse half. The committed sheet carried only the four `epic-remaining-*`
//      variants that happened to exist at extraction time, so when an epic first entered review its
//      dashboard tile rendered with NO `border-left-color` rule at all. Nothing failed; the tile was just
//      quietly unstyled until somebody regenerated.
//
// The fix is to seed the CLOSED DOMAIN a class is drawn from rather than the subset observed today. A rule
// is still carried only when EVERY class it names is present, so seeding is self-limiting: seeding
// `deferred` cannot carry `.req-card.deferred` onto a migrated page that has no `.req-card`.
//
// ⚠️ THIS IS A HAND-MAINTAINED DUPLICATE of vocabularies authored in C# (`StatusStyles.LegendStages`,
// `SprintTemplater.BoardColumns`), and duplicating a list is the same class of defect it fixes — it goes
// stale when a stage is added. It is deliberate and temporary: the durable form has the C# side publish
// these domains into the IR so this file reads them instead. See ADR 0026. Until that lands, a new stage
// must be added HERE as well as in `StatusStyles`.

/**
 * Every canonical lifecycle stage token, from `StatusStyles.LegendStages` — the superset of `StoryStages`
 * and `EpicStages`. These appear BARE alongside a base class (`.status-badge.review`, `.donut-seg.done`,
 * `.sprint-card.active`, `.now-next-card.ready`), so seeding the tokens themselves covers those families.
 */
export const STAGES = [
  'pending', 'drafted', 'ready', 'active', 'review', 'done',
  'deferred', 'unmapped', 'retired', 'unrecognized',
]

/**
 * Families that build a COMPOUND class name from a stage token. Each entry is a `%s` template; the stage is
 * substituted in. Unlike the bare tokens above, no amount of markup harvesting finds `.epic-remaining-review`
 * unless an epic is in review at extraction time — which is exactly the bug.
 */
export const STAGE_CLASS_TEMPLATES = [
  'epic-remaining-%s', // Charts.cs — dashboard "remaining work" tiles, keyed on ForEpicWithRetrospective
  'dn-%s-item', //        Charts.cs — donut legend row
  'sb-%s-item', //        Charts.cs — stacked-bar legend row
  'sb-%s-sw', //          Charts.cs — stacked-bar legend swatch
  // `sb-%s` (bare) is the wedge FILL itself (`.sb-done { fill: var(--status-done); } …`, specscribe.css
  // ~3237-3247) — distinct from the `-item`/`-sw` legend forms above. Story 20.7 deleted the server-rendered
  // `<svg class="sunburst">` that used to carry this class on a real element; since then it is applied ONLY
  // to a JS-only hidden probe node (`specscribe.js` `tokenFor()` / `.ss-hierarchy-probe`) that `getComputedStyle`
  // reads back out of the cascade. No harvest of real markup will ever find it — without this seed the rule is
  // dropped and every wedge falls back to the SVG default fill, black. [incident: sunburst rendered all-black]
  'sb-%s',
  'list-row-accent-%s', // StatusStyles.AdrAccentToken — list-row left accent bar
]

/**
 * Classes emitted only when a data condition holds on a MIGRATED surface, and therefore absent from a
 * harvest taken while that condition is false. Not a general "every empty state" list — these are the ones
 * the four migrated families can render.
 *
 * `sprint-lane-empty` is the worked example: `SprintTemplater` emits it only `if (col.Count == 0)`, so a
 * board with every lane populated drops twelve declarations of dashed-border empty-state styling from the
 * generated layer, and the next genuinely empty lane renders bare.
 */
export const CONDITIONAL_CLASSES = [
  'sprint-lane-empty', //   SprintTemplater — a board column with no cards
  'sprint-filter-empty', // SprintTemplater — the epic filter emptied a lane
  'sprint-lane-more', //    SprintTemplater — per-column cap overflow
  'unplanned-card', //      SprintTemplater — ledger entries with no story artifact
  'chart-empty', //         Charts — a chart with nothing to plot
  // Sunburst-only wedge tokens, not part of `StatusStyles.LegendStages` so not covered by `STAGES`/
  // `sb-%s` above. Same probe-only visibility gap as `sb-%s`: never on real markup, only on the hidden
  // `.ss-hierarchy-probe` node `specscribe.js` reads colors back from. [same incident as `sb-%s`]
  'sb-seg', //            base wedge stroke/hover rule
  'sb-noplan', //          middle-ring story with no task plan (dashed, transparent fill)
  'sb-followup-open', //   follow-up wedge, open
  'sb-followup-done', //   follow-up wedge, done
  'sb-unplanned', //       unplanned/direct-change wedge
  // Ownership dimension's top-author palette (Charts.OwnershipTopAuthorsLegend /
  // HierarchyExplorer.Projectors's `owner-author-%d` ClassPrefix). `owner-author-{i}` DOES appear on real
  // markup (`ownership-wedge`/`ownership-legend-swatch`), but which indices render depends on
  // `GitMetrics.BuildTopAuthors`'s live `--deep-git` commit history — the exact "FALSE DRIFT / SILENT STYLE
  // LOSS" class this file's banner comment warns about for project-DATA-driven classes, and observed here:
  // `owner-author-2`'s rule was dropped when a harvest ran one commit short of the count that would include
  // a second author in the top-N window. `Charts.OwnershipTopAuthorPaletteSize` bounds the palette to 7
  // slots (0-6) regardless of how many distinct authors exist, so seeding the whole bound — not just
  // whatever the harvest happened to observe — is closed-domain, same as STAGES. [incident: check:ir-content
  // failed in CI with `+.ir-content .ownership-legend-swatch.owner-author-2`, absent from a local harvest run
  // one commit behind]
  'owner-author-0',
  'owner-author-1',
  'owner-author-2',
  'owner-author-3',
  'owner-author-4',
  'owner-author-5',
  'owner-author-6',
  // ── Cross-FRAMEWORK markup ────────────────────────────────────────────────────────────────────────────
  // The epics index's milestone bands (HtmlRenderAdapter.AppendMilestoneBands), emitted only when the
  // ingested project's framework HAS a milestone level above the epic — GSD Core's `v1.0`/`v2.0` and its
  // `Backlog` group. BMad has no such level, so `EpicsModel.Milestones` is empty for it and the renderer
  // takes the chip-section branch instead.
  //
  // ⚠️ This is a NEW SUB-CATEGORY of the gap this list exists for, and it is worth naming because every
  // remaining framework epic (11, 12.3, 13, 14, 15) will hit it. The entries above are absent because a data
  // condition happens to be false in THIS repo right now; these are absent because the extraction corpus is
  // this repository's own IR, and this repository is a BMad project. No amount of regenerating fixes that —
  // markup only a NON-BMAD repo can produce is structurally invisible to a harvest of a BMad one.
  //
  // Measured, not assumed: with the stylesheet edit in place and the documented regeneration order followed
  // exactly, all five `.milestone-band*` rules were pruned and `check:ir-content` stayed GREEN — the bands
  // would have shipped unstyled on a real GSD site with no gate able to see it. That is the same silent-loss
  // failure as the sunburst-black-fill and `owner-author-2` incidents above, reached by a different route.
  // Seeding stays self-limiting: `.milestone-band .epic-overview` is carried only because `epic-overview`
  // is genuinely present too. [Story 12.2 Task 8; ADR 0038]
  'milestone-band',
  'milestone-band-header',
  'milestone-band-name',
  'milestone-band-meta',
  'milestone-band-empty',
  // ── Applied by specscribe.js at RUNTIME, INSIDE `.ir-content` ─────────────────────────────────────────
  // The same probe-only visibility gap as `sb-%s` above, reached by a different route: these are stamped
  // onto live DOM by the hierarchy explorer and the details rail after the page is rendered, so no harvest
  // of static markup can ever find them. They belong in the SCOPED layer — unlike the tooltip families in
  // `RUNTIME_BODY_CLASSES`, every one of these nodes is a descendant of the page's `.ir-content` wrapper.
  //
  // What their absence cost, all of it silent and all of it with every gate green [incident 2026-08-06]:
  'is-related-current', //     Story 20.3's details rail. `specscribe.js` toggles it on the ONE card matching
  //                          the selected node; the pruned rule was the `display: block` that un-hides it.
  //                          Its `display: none` sibling survived, so selecting a node on the dashboard hid
  //                          every card and revealed nothing — the rail went BLANK on select.
  'ss-hierarchy-sector', //    stamped on every Plotly sector; the tooltip/hover/focus hook (`SEG`)
  'ss-hierarchy-probe', //     the hidden colour-probe host. `.ss-hierarchy-probe .sb-noplan` gives no-plan a
  //                          real chart fill; without it `fillFor` falls through to the STROKE and a no-plan
  //                          wedge came out byte-identical to a Pending one.
  'ss-hierarchy-sw', //        the legend's hatched swatches — the KEY that explains the hatching
  'ss-hierarchy-crumb', //     the JS-built drill breadcrumb
  'ss-hierarchy-crumb-current',
  'ss-hierarchy-crumb-open',
  // Plotly's OWN class names, on the sector nodes it emits. `.ss-hierarchy g.slice path.surface:focus`
  // is the keyboard focus ring; it named two classes this repo never writes, so it was always dropped.
  'slice',
  'surface',
]

/** Every class name the seeding above contributes, flattened. */
export const conditionalClassNames = () => [
  ...STAGES,
  ...STAGE_CLASS_TEMPLATES.flatMap((t) => STAGES.map((s) => t.replace('%s', s))),
  ...CONDITIONAL_CLASSES,
]

// ── A small, comment-aware CSS reader ────────────────────────────────────────────────────────────────────
//
// No npm CSS parser: `web/` runs on nuxt + vue + vue-router and the vendored Plotly build, and ADR 0010's
// zero-dependency posture is a deliberate project property. This reads what this one stylesheet actually
// contains — nested at-rules one level deep, no `@supports` chains, no custom syntax.

/** Strips `/* … *​/` comments, tracking string state so a `/*` inside a url() or content string is safe. */
export function stripComments(css) {
  let out = ''
  let i = 0
  let quote = null
  while (i < css.length) {
    const ch = css[i]
    if (quote) {
      out += ch
      if (ch === '\\') {
        out += css[i + 1] ?? ''
        i += 2
        continue
      }
      if (ch === quote) quote = null
      i += 1
      continue
    }
    if (ch === '"' || ch === "'") {
      quote = ch
      out += ch
      i += 1
      continue
    }
    if (ch === '/' && css[i + 1] === '*') {
      const end = css.indexOf('*/', i + 2)
      if (end < 0) throw new Error(`${SOURCE_LABEL}: unterminated CSS comment at offset ${i}`)
      // Keep newlines so reported line numbers stay true to the source.
      out += css.slice(i, end + 2).replace(/[^\n]/g, '')
      i = end + 2
      continue
    }
    out += ch
    i += 1
  }
  return out
}

/**
 * Top-level blocks, in source order.
 *
 * Each is `{ kind: 'rule' | 'at', prelude, body, startLine, endLine }`. `at` blocks (`@media`, `@keyframes`,
 * `@supports`) keep their raw body; rules inside a conditional at-rule are re-read recursively by the
 * caller so they can be filtered and scoped individually.
 */
export function readBlocks(css) {
  const blocks = []
  let i = 0
  let preludeStart = 0

  const lineAt = (offset) => css.slice(0, offset).split('\n').length

  while (i < css.length) {
    const ch = css[i]
    if (ch === '{') {
      const prelude = css.slice(preludeStart, i).trim()
      let depth = 1
      let j = i + 1
      let quote = null
      while (j < css.length && depth > 0) {
        const c = css[j]
        if (quote) {
          if (c === '\\') j += 1
          else if (c === quote) quote = null
        } else if (c === '"' || c === "'") quote = c
        else if (c === '{') depth += 1
        else if (c === '}') depth -= 1
        j += 1
      }
      if (depth !== 0) throw new Error(`${SOURCE_LABEL}: unbalanced braces after "${prelude.slice(0, 60)}"`)
      blocks.push({
        kind: prelude.startsWith('@') ? 'at' : 'rule',
        prelude,
        body: css.slice(i + 1, j - 1),
        startLine: lineAt(preludeStart + (css.slice(preludeStart).length - css.slice(preludeStart).trimStart().length)),
        endLine: lineAt(j - 1),
      })
      i = j
      preludeStart = i
      continue
    }
    if (ch === ';' && css.slice(preludeStart, i).trim().startsWith('@')) {
      // A statement at-rule (`@charset`, `@import`). Recorded so nothing is silently skipped.
      blocks.push({
        kind: 'statement',
        prelude: css.slice(preludeStart, i).trim(),
        body: '',
        startLine: lineAt(preludeStart),
        endLine: lineAt(i),
      })
      i += 1
      preludeStart = i
      continue
    }
    i += 1
  }
  return blocks
}

// ── Selector usage matching ──────────────────────────────────────────────────────────────────────────────

/**
 * The identifiers a compound selector depends on: class names, ids and element names.
 *
 * A rule is kept when EVERY class and id it names is present somewhere in the migrated markup. Requiring
 * all of them (rather than any) is what keeps the layer bounded — `.chart-panel .legend-swatch` should not
 * be carried onto a page that has chart panels but no legends.
 */
export function selectorTokens(selector) {
  const cleaned = selector
    .replace(/\[[^\]]*\]/g, ' ') // attribute selectors: matched separately, see attributesUsed
    .replace(/::?[a-z-]+(\([^)]*\))?/gi, ' ') // pseudo-classes/elements, including :is(...)/:has(...) args
  return {
    classes: [...cleaned.matchAll(/\.(-?[_a-zA-Z][\w-]*)/g)].map((m) => m[1]),
    ids: [...cleaned.matchAll(/#(-?[_a-zA-Z][\w-]*)/g)].map((m) => m[1]),
  }
}

/** Attribute names a selector tests for, from both the compound and any functional-pseudo argument. */
export function selectorAttributes(selector) {
  return [...selector.matchAll(/\[\s*([-\w]+)/g)].map((m) => m[1])
}

/**
 * Does the markup use everything this selector needs?
 *
 * CLASSES and IDS bound the extraction: every one a selector names must appear in the migrated families'
 * markup, or the rule is not carried.
 *
 * ATTRIBUTES deliberately do NOT bound it. Nearly every attribute selector in this stylesheet expresses
 * RUNTIME STATE — `[data-ss-hierarchy-boot]`, `[data-hierarchy-ready]`, `[data-hierarchy-failed]`, `[open]`,
 * `[aria-expanded="true"]` — which by definition is absent from server-rendered markup. Requiring them
 * would drop precisely the interaction CSS the Hierarchy Explorer's anti-flash handshake depends on (AC #7),
 * and it would do it silently: the page would render, the chart would mount, and the fallback SVG would
 * flash first with nothing in any test able to see it.
 *
 * Selectors that name no class, id or attribute (bare element selectors like `table td`) are matched on
 * element name. `:root` is normalized to `html` first so root-anchored rules reach `scopeSelector`, which
 * is where the decision about what can and cannot be scoped actually belongs.
 */
/**
 * WHICH tokens made `selectorIsUsed` say no — the companion this file went four incidents without.
 *
 * `selectorIsUsed` answers a yes/no that nothing records: a dropped rule leaves no trace in the manifest,
 * no line in the log and no failing gate, so "silently absent from the shipped site" has been this layer's
 * repeated failure mode (black sunburst fills, `owner-author-2`, the Code Map's id-bearing filter, and the
 * tooltip/details-rail loss this function was added for). Reporting the CAUSE turns each of those from a
 * live-browser discovery into a line a human can read at extraction time.
 *
 * Returns `{ classes, ids }` of the tokens that are absent. Empty arrays for both means the selector was
 * carried, or was rejected on element name rather than on a token.
 */
export function missingTokens(selector, used) {
  const normalized = selector.replace(/:root\b/g, 'html')
  const { classes, ids } = selectorTokens(normalized)
  return {
    classes: classes.filter((c) => !used.classes.has(c)),
    ids: ids.filter((id) => !used.ids.has(id)),
  }
}

export function selectorIsUsed(selector, used) {
  const normalized = selector.replace(/:root\b/g, 'html')
  const { classes, ids } = selectorTokens(normalized)
  const attrs = selectorAttributes(normalized)

  for (const c of classes) if (!used.classes.has(c)) return false
  for (const id of ids) if (!used.ids.has(id)) return false

  if (classes.length || ids.length || attrs.length) return true

  const elements = [...normalized.matchAll(/(^|[\s>+~,(])([a-zA-Z][\w-]*)/g)].map((m) => m[2].toLowerCase())
  return elements.length > 0 && elements.every((e) => used.elements.has(e))
}

// ── Scoping ──────────────────────────────────────────────────────────────────────────────────────────────

/** Selectors that address the document root. They cannot be nested under `.ir-content`. */
const ROOT_HEADS = /^(:root|html|body|\*)\b/

/**
 * Nests one selector under `.ir-content`.
 *
 * Root-anchored selectors get the scope inserted AFTER their root part, so state selectors keep working:
 * `:root[data-ss-hierarchy-boot] .chart-panel …` becomes
 * `:root[data-ss-hierarchy-boot] .ir-content .chart-panel …` — the anti-flash boot rules depend on exactly
 * this and would be silently dead if the scope were prepended instead.
 *
 * Returns null for a selector that addresses ONLY the root (`body { … }`), which has no descendant to
 * scope. `assets/base.css` already supplies the app's page-level typography and background; those rules are
 * listed in the manifest as dropped rather than dropped quietly.
 */
export function scopeSelector(selector) {
  const s = selector.trim()
  if (!ROOT_HEADS.test(s)) return `${SCOPE} ${s}`

  const head = /^((?::root|html|body|\*)(?:\[[^\]]*\]|[:.#][\w-]+(?:\([^)]*\))?)*)/.exec(s)
  const rest = s.slice(head[1].length).trim()
  if (rest === '') return null
  const combinator = /^[>+~]/.test(rest) ? '' : ' '
  return `${head[1]} ${SCOPE}${combinator ? ' ' : ''}${rest}`
}

export function scopePrelude(prelude) {
  const scoped = prelude
    .split(',')
    .map((s) => s.trim())
    .filter(Boolean)
    .map(scopeSelector)
    .filter(Boolean)
  return scoped.length ? scoped.join(',\n') : null
}

/** The committed generated sheet, line-ending-normalized. Null when it does not exist yet. */
export function readCommitted(file) {
  try {
    return readFileSync(file, 'utf8').replace(/\r\n/g, '\n')
  } catch (err) {
    if (err.code === 'ENOENT') return null
    throw err
  }
}
