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

/** Repo-relative label — never an absolute machine path, which would differ per checkout. */
export const SOURCE_LABEL = 'src/SpecScribe/assets/specscribe.css'

/** The scope every emitted rule is nested under. Falls through onto `PageShell`'s root element. */
export const SCOPE = '.ir-content'

/**
 * The families whose markup drives the extraction. [owner decision D4]
 *
 * Deliberately NOT every page in the manifest: driving usage off all 1,042 pages would pull in most of the
 * monolith and turn a bounded layer back into a wholesale import. Pass-through pages are 23.4's, and get
 * whatever overlap the migrated families already paid for — `extract-ir-content.mjs` reports that coverage
 * as a number rather than leaving it implied.
 */
export const MIGRATED = {
  dashboard: (p) => p === 'index.html',
  epicsIndex: (p) => p === 'epics.html',
  epicDetail: (p) => /^epics\/epic-[^/]+\.html$/.test(p),
  storyDetail: (p) => /^epics\/story-[^/]+\.html$/.test(p),
}

export const isMigrated = (path) => Object.values(MIGRATED).some((f) => f(path))

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
