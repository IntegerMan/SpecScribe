// Builds the generated `ir-content.css` and its manifest. Shared by `extract:ir-content` (which writes
// them) and `check:ir-content` (which re-derives and diffs). One builder, so the gate can never be checking
// something the extractor would not have produced. [Story 23.3 AC #6]

import { readFileSync } from 'node:fs'
import {
  isMigrated,
  readBlocks,
  scopePrelude,
  selectorAttributes,
  selectorIsUsed,
  selectorTokens,
  stripComments,
  SCOPE,
  SOURCE_CSS,
  SOURCE_LABEL,
} from './ir-content-lib.mjs'

/** The adapter is the ONE reader of the IR's shape — the harnesses go through it too, not around it. */
async function loadIr() {
  return import('../ir/adapter.ts')
}

/** Class names, ids, attribute names and element names present in a run of markup. */
function harvest(html, into) {
  for (const m of html.matchAll(/<([a-zA-Z][\w-]*)((?:"[^"]*"|'[^']*'|[^>"'])*)>/g)) {
    into.elements.add(m[1].toLowerCase())
    const attrs = m[2]
    for (const a of attrs.matchAll(/([-\w:]+)(?:\s*=\s*(?:"([^"]*)"|'([^']*)'))?/g)) {
      const name = a[1].toLowerCase()
      into.attributes.add(name)
      const value = a[2] ?? a[3] ?? ''
      if (name === 'class') for (const c of value.split(/\s+/)) if (c) into.classes.add(c)
      else if (name === 'id' && value) into.ids.add(value)
    }
  }
}

export async function buildIrContentCss() {
  const ir = await loadIr()

  // ── 1. What the migrated families actually render ──────────────────────────────────────────────────────
  const used = { classes: new Set(), ids: new Set(), attributes: new Set(), elements: new Set() }
  const other = { classes: new Set(), ids: new Set(), attributes: new Set(), elements: new Set() }

  let migratedPages = 0
  for (const path of ir.site.paths) {
    const page = ir.page(path)
    const r = page.region
    // A degraded page carries no <main> landmark and therefore no body to harvest rules from. Skip it rather
    // than folding its raw nav markup into the used/other sets, which would attribute nav-only selectors to a
    // page that renders nothing. [Story 22.4 code review — owner decision DR2]
    if (r.degraded) continue
    const markup = `${r.navHtml}${r.wayfindingHtml}${r.mainInnerHtml}`
    if (isMigrated(path)) {
      migratedPages += 1
      harvest(markup, used)
    } else {
      harvest(markup, other)
    }
  }
  // The shell contributes these, and they are template-authored rather than injected — the region's markup
  // starts INSIDE `<main>`, so nothing in it can tell the extractor that `main`, `body` or `html` exist.
  used.classes.add('skip-link')
  used.classes.add('ir-content')
  used.elements.add('html')
  used.elements.add('body')
  used.elements.add('main')

  // ── 2. Carry the matching rules ────────────────────────────────────────────────────────────────────────
  const source = readFileSync(SOURCE_CSS, 'utf8').replace(/\r\n/g, '\n')
  const blocks = readBlocks(stripComments(source))

  const carried = []
  const manifestRules = []
  const stats = { sourceRules: 0, carriedRules: 0, carriedSelectors: 0, droppedUnused: 0, droppedRoot: 0 }
  const keyframeBlocks = new Map()

  /** Filters + scopes one rule block. Returns the emitted text, or null when nothing survived. */
  function takeRule(block, insideAt) {
    stats.sourceRules += 1
    const selectors = block.prelude
      .split(',')
      .map((s) => s.trim())
      .filter(Boolean)
    const keep = selectors.filter((s) => selectorIsUsed(s, used))
    if (keep.length === 0) {
      stats.droppedUnused += 1
      return null
    }
    const scoped = scopePrelude(keep.join(','))
    if (!scoped) {
      // Every surviving selector addressed the document root and had no descendant to scope.
      stats.droppedRoot += 1
      manifestRules.push({
        selector: keep.join(', '),
        carried: false,
        reason: 'root-level rule — no descendant to scope under .ir-content; see web/assets/base.css',
      })
      return null
    }
    stats.carriedRules += 1
    stats.carriedSelectors += keep.length
    manifestRules.push({
      selector: keep.join(', '),
      carried: true,
      ...(insideAt ? { within: insideAt } : {}),
      ...(keep.length < selectors.length
        ? { note: `${selectors.length - keep.length} unused selector(s) in the source rule were not carried` }
        : {}),
    })
    return `${scoped} {${block.body.replace(/\n+$/, '\n')}}`
  }

  /**
   * Walks one level of blocks. `at` is the conditional at-rule prelude they sit inside, or null at the top
   * level. Returns the emitted text for that level.
   *
   * ⚠️ `@keyframes` in this stylesheet are NESTED inside `@media (prefers-reduced-motion: …)` blocks, not
   * declared at the top level. A top-level-only keyframe scan finds zero of them and emits a sheet whose
   * `animation:` declarations all name nothing — every entrance silently dead, and nothing in a markup
   * comparison able to notice.
   */
  function walk(level, at) {
    const out = []
    for (const block of level) {
      if (block.kind === 'statement') continue

      if (block.kind === 'rule') {
        const text = takeRule(block, at)
        if (text) out.push(text)
        continue
      }

      const prelude = block.prelude
      if (/^@keyframes\b/i.test(prelude)) {
        const name = prelude.replace(/^@keyframes\s+/i, '').trim()
        // Keyed by name AND condition: the same animation is redefined per media condition, and collapsing
        // them would silently pick one at random. NUL is the separator because it cannot occur in a CSS
        // identifier or media condition, so the two halves can never be ambiguous — written as the six-character
        // backslash-u escape below, NEVER as a raw NUL byte: a literal NUL makes Git sniff this whole file
        // as binary, costing it diffs in review and exempting it from the .gitattributes text normalization.
        keyframeBlocks.set(`${at ?? ''}\u0000${name}`, { name, at, block })
        continue
      }
      if (/^@(media|supports|layer|container)\b/i.test(prelude)) {
        const inner = walk(readBlocks(block.body), prelude)
        if (inner.length) out.push(`${prelude} {\n${inner.join('\n\n')}\n}`)
        continue
      }
      // @font-face and friends: carried whole — they declare a resource, not a selector match.
      out.push(`${prelude} {${block.body}}`)
      manifestRules.push({ selector: prelude, carried: true })
    }
    return out
  }

  carried.push(...walk(blocks, null))

  // ── 3. Keyframes, only those the carried rules animate ─────────────────────────────────────────────────
  const body = carried.join('\n')
  const animated = new Set()
  for (const m of body.matchAll(/animation(?:-name)?\s*:\s*([^;}]+)/g)) {
    for (const tok of m[1].split(/[,\s]+/)) if (tok) animated.add(tok.trim())
  }
  const keyframes = []
  const byCondition = new Map()
  for (const { name, at, block } of keyframeBlocks.values()) {
    if (!animated.has(name)) continue
    const text = `@keyframes ${name} {${block.body}}`
    if (at) byCondition.set(at, [...(byCondition.get(at) ?? []), text])
    else keyframes.push(text)
    manifestRules.push({
      selector: `@keyframes ${name}`,
      carried: true,
      ...(at ? { within: at } : {}),
    })
    stats.carriedKeyframes = (stats.carriedKeyframes ?? 0) + 1
  }
  for (const [at, texts] of byCondition) keyframes.push(`${at} {\n${texts.join('\n\n')}\n}`)
  stats.carriedKeyframes ??= 0

  // ── 4. Emit ────────────────────────────────────────────────────────────────────────────────────────────
  //
  // The banner is the ONLY comment in the output. Source comments are stripped rather than carried, which
  // removes the `*`+`/`-inside-a-comment hazard by construction — that exact sequence once closed a comment
  // early in this stylesheet and silently killed ~1,000 rules.
  const banner = [
    '/* GENERATED FILE - DO NOT EDIT.',
    ` * Extracted from ${SOURCE_LABEL} by \`npm run extract:ir-content\` (Story 23.3 AC #6).`,
    ' *',
    ' * The IR ships markup authored against that 7,041-line stylesheet; this app imports only the token',
    ' * bridge. This layer carries the rules the four MIGRATED families actually use, re-nested under',
    ` * \`${SCOPE}\` so they cannot reach a template-authored component. It is TRANSITIONAL: Story 23.4`,
    ' * retires it, and web/assets/ir-content.manifest.json names every source rule it carries.',
    ' *',
    ' * Re-run `npm run extract:ir-content` after any change to the C# stylesheet or to what the migrated',
    ' * surfaces render. `npm run check:ir-content` fails the build when this file and the source diverge.',
    ' */',
  ].join('\n')

  const css = `${banner}\n\n${[...carried, ...keyframes].join('\n\n')}\n`

  // ── 5. Pass-through coverage, reported rather than implied ─────────────────────────────────────────────
  const otherOnly = [...other.classes].filter((c) => !used.classes.has(c))
  const passThroughCoveredPct =
    other.classes.size === 0
      ? 100
      : Math.round(((other.classes.size - otherOnly.length) / other.classes.size) * 100)

  const sourceBytes = Buffer.byteLength(source)
  const outBytes = Buffer.byteLength(css)

  /**
   * ⚠️ Only fields that describe the CARRIED LAYER are committed here. Anything that describes the SOURCE
   * stylesheet as a whole, or the corpus as a whole, is computed and reported but deliberately not written.
   *
   * This manifest is a COMMITTED artifact that `npm run check:ir-content` compares byte-for-byte, and
   * Story 23.5 put that comparison into CI. The rule that keeps it honest: a field belongs here only if
   * changing it implies the emitted `ir-content.css` changed too. A field that can move on its own turns
   * the gate red on a commit that could not possibly have affected the layer — and a gate that cannot stay
   * green teaches people to re-run the extractor on reflex, which is exactly how a real drift gets
   * committed unnoticed.
   *
   * Two rounds of fields have failed that rule and been removed:
   *
   *   1. WHOLE-CORPUS [Story 23.5 AC #8] — `migratedPages`, `totalPages`, `passThroughUncoveredClasses`
   *      are functions of the ENTIRE ~1,100-page corpus, so they moved whenever anybody added a document.
   *
   *   2. WHOLE-SOURCE — `sourceRules`, `droppedUnused`, `sourceBytes`, and the per-rule `lines` span.
   *      These count or locate rules the layer does NOT carry, so ANY edit to `specscribe.css` moved them:
   *      deleting 38 unused rules in commit 06b300c shifted every line span in the file and reddened CI
   *      with an 865-line diff while `ir-content.css` stayed byte-identical. Line spans are the worse
   *      offender of the two — they are also the anchor this project already learned not to cite by, and
   *      `selector` (plus `within`) identifies a rule without going stale.
   *
   * All of them are still COMPUTED and still reported by `npm run extract:ir-content`'s console summary,
   * which is where they are actually useful (at extraction time, to a human). Regenerate to see them.
   *
   * Be honest about what this does NOT fix: `rules` and the emitted CSS still depend on which classes the
   * FOUR MIGRATED FAMILIES use, so a dashboard/epics markup change can still legitimately move this file.
   * That dependence is inherent to how Story 23.3 derives the layer and is the gate working as designed —
   * it is narrow (4 families) where the removed fields were broad (every page, or every source rule).
   */
  const manifest = {
    generatedBy: 'web/scripts/extract-ir-content.mjs',
    source: SOURCE_LABEL,
    scope: SCOPE,
    transitional: 'Story 23.4 retires this layer. Every entry below is a rule it has to account for.',
    migratedFamilies: ['index.html', 'epics.html', 'epics/epic-{N}.html', 'epics/story-{id}.html'],
    stats: {
      carriedRules: stats.carriedRules,
      carriedSelectors: stats.carriedSelectors,
      carriedKeyframes: stats.carriedKeyframes,
      droppedRoot: stats.droppedRoot,
      generatedBytes: outBytes,
    },
    rules: manifestRules,
  }

  return {
    css,
    manifest,
    stats: {
      ...stats,
      migratedPages,
      totalPages: ir.site.paths.length,
      passThroughCoveredPct,
      reductionPct: Math.round((1 - outBytes / sourceBytes) * 100),
    },
  }
}

// Re-exported so the CLI can report on them without reaching into the lib twice.
export { selectorAttributes, selectorTokens }
