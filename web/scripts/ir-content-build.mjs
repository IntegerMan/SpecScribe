// Builds the generated `ir-content.css` and its manifest. Shared by `extract:ir-content` (which writes
// them) and `check:ir-content` (which re-derives and diffs). One builder, so the gate can never be checking
// something the extractor would not have produced. [Story 23.3 AC #6]

import { readFileSync } from 'node:fs'
import {
  conditionalClassNames,
  isMigrated,
  isRuntimeBodyClass,
  isSharedPrimitive,
  missingTokens,
  readBlocks,
  scopePrelude,
  selectorAttributes,
  selectorIsUsed,
  selectorTokens,
  stripComments,
  RUNTIME_BODY_CLASSES,
  SCOPE,
  SHARED_PRIMITIVES,
  SOURCE_CSS,
  SOURCE_LABEL,
} from './ir-content-lib.mjs'

/** The adapter is the ONE reader of the IR's shape — the harnesses go through it too, not around it. */
async function loadIr() {
  return import('../ir/adapter.ts')
}

/** Class names, ids, attribute names and element names present in a run of markup.
 * Exported for `test/ir-content-harvest.test.mjs` — see the braces note below for why this one needs a unit test
 * of its own rather than relying on `check:ir-content`, which shares this function and so cannot disagree with it. */
export function harvest(html, into) {
  for (const m of html.matchAll(/<([a-zA-Z][\w-]*)((?:"[^"]*"|'[^']*'|[^>"'])*)>/g)) {
    into.elements.add(m[1].toLowerCase())
    const attrs = m[2]
    for (const a of attrs.matchAll(/([-\w:]+)(?:\s*=\s*(?:"([^"]*)"|'([^']*)'))?/g)) {
      const name = a[1].toLowerCase()
      into.attributes.add(name)
      const value = a[2] ?? a[3] ?? ''
      // ⚠️ BRACES ARE LOAD-BEARING. This was written brace-free as
      //     if (name === 'class') for (…) if (c) into.classes.add(c)
      //     else if (name === 'id' && value) into.ids.add(value)
      // where the `else` binds to the INNER `if (c)`, not to `if (name === 'class')` — the dangling-else. The id
      // branch therefore ran only when the attribute was `class` AND a split token was falsy, i.e. never, so
      // `into.ids` stayed EMPTY for the whole site and `selectorIsUsed` dropped every rule naming an id.
      //
      // What that silently killed: the Code Map's pure-CSS spec/test filter
      // (`#cm-exclude-spec:checked ~ …`, 14 rules) never reached `ir-content.css`, so the two checkboxes on the
      // shipped page did nothing at all — the no-JS filter guarantee of Story 20.9 D2/D3, absent from the
      // rendered site while every gate stayed green. Found 2026-08-01 by inspecting computed styles in a live
      // browser after the filter failed to hide anything; no test could see it, because the extractor and the
      // gate that checks it share this function.
      if (name === 'class') {
        for (const c of value.split(/\s+/)) if (c) into.classes.add(c)
      } else if (name === 'id' && value) {
        into.ids.add(value)
      }
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
    // ⚠️ `trailingHtml` is part of the harvest, and leaving it out was a real defect. [Story 23.4]
    //
    // It is the region's post-`</main>` content — normally empty, but on `deep-analytics.html` it is the
    // `:target` lightbox. Harvesting only the body meant `.coupling-lightbox { display: none }` and its
    // `:target` companion were never carried, so once the lightbox finally reached the IR the overlay rendered
    // PERMANENTLY OPEN: a 526 px panel sitting in the page instead of a dialog that opens on demand. Present in
    // the DOM, correct in the region, invisible to every harness, and visibly wrong to a reader.
    //
    // This is the third layer that dropped the same content (C# slicer → TS splitter → this harvest). Any code
    // that reconstructs "the region" from its parts must use ALL the parts.
    const markup = `${r.navHtml}${r.wayfindingHtml}${r.mainInnerHtml}${r.trailingHtml}`
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

  // Classes the migrated families CAN render but happen not to be rendering in this IR — status stages with
  // no member today, empty states whose lane is currently full. Seeded from their closed domains rather than
  // harvested, because harvesting them makes both this file and the gate a function of project DATA: the
  // sheet loses `.epic-remaining-review` styling until an epic enters review, and CI reddens the day one
  // does. See the CONDITIONAL_CLASSES block in ir-content-lib.mjs for the full rationale and ADR 0026 for
  // the durable fix. This is what makes the layer a function of the TEMPLATES, not of the sprint.
  for (const name of conditionalClassNames()) used.classes.add(name)

  // The unscoped runtime allowlist is seeded HERE TOO, and it is not redundant with the partition further
  // down. `selectorIsUsed` runs FIRST and drops a selector naming any unharvested class — so without this,
  // `.ss-tooltip` never survives long enough for `isRuntimeBodyClass` to route it anywhere, and the layer
  // emits only the members that happen to be harvested anyway. Measured exactly that on the first run: 7
  // rules emitted, every one of them `codemap-card*` (server-built into `data-tip-html`, therefore visible),
  // and the tooltip itself — the whole reason the layer exists — still missing. [ADR 0039]
  for (const name of RUNTIME_BODY_CLASSES) used.classes.add(name)

  // ── 2. Carry the matching rules ────────────────────────────────────────────────────────────────────────
  const source = readFileSync(SOURCE_CSS, 'utf8').replace(/\r\n/g, '\n')
  const blocks = readBlocks(stripComments(source))

  const carried = []
  const manifestRules = []
  /** Emitted CSS text for the unscoped shared layer, and its manifest entries. [ADR 0029] */
  const sharedCarried = []
  const manifestSharedRules = []
  /** The second unscoped layer: nodes specscribe.js attaches outside `.ir-content`. [ADR 0039] */
  const runtimeCarried = []
  const manifestRuntimeRules = []
  /**
   * Every selector dropped for a token the harvest never saw, with the token that caused it.
   *
   * ⚠️ REPORTED, never committed to the manifest — deliberately, and the reason is the committed-fields rule
   * documented at the manifest below. This list is a function of the WHOLE SOURCE stylesheet, so it moves on
   * any `specscribe.css` edit; putting it in the gated artifact would redden CI on commits that cannot have
   * changed the emitted layer, which is precisely how people learn to re-run the extractor on reflex. It goes
   * to the console and to `web/measurements/`, which is where a human actually reads it. [ADR 0039]
   */
  const dropped = []
  const stats = {
    sourceRules: 0,
    carriedRules: 0,
    carriedSelectors: 0,
    droppedUnused: 0,
    droppedRoot: 0,
    sharedRules: 0,
    runtimeRules: 0,
  }
  const keyframeBlocks = new Map()

  /**
   * Filters + scopes one rule block. Returns `{ scoped, shared }` — either may be null.
   *
   * A source rule can contribute to BOTH layers, because one prelude can carry several selectors: if
   * `specscribe.css` ever wrote `.pill, .list-row-chip { … }`, the shared half goes out unscoped and the rest
   * stays scoped, with the split recorded. Splitting per selector rather than per rule is what keeps the
   * allowlist's all-or-nothing promise honest at the granularity CSS actually applies it. [ADR 0029]
   */
  function takeRule(block, insideAt) {
    stats.sourceRules += 1
    const selectors = block.prelude
      .split(',')
      .map((s) => s.trim())
      .filter(Boolean)
    // Record the CAUSE of every drop, per selector, before collapsing to a count. A selector rejected on
    // element name rather than on a class/id contributes empty arrays and is filtered out downstream.
    for (const s of selectors) {
      if (selectorIsUsed(s, used)) continue
      const miss = missingTokens(s, used)
      if (miss.classes.length === 0 && miss.ids.length === 0) continue
      dropped.push({
        selector: s,
        ...(insideAt ? { within: insideAt } : {}),
        missingClasses: miss.classes,
        ...(miss.ids.length ? { missingIds: miss.ids } : {}),
      })
    }

    // The usage prune asks "does the IR corpus render this class?" — the right question for the SCOPED layer,
    // whose whole job is styling injected markup. It is the WRONG question for the unscoped layers, and
    // asking it of them was a real defect. [Story 23.2 review 2026-08-07]
    //
    // `used` is harvested from IR page markup only, never from Vue templates. So on a project whose IR
    // happens to render no `.pill`, the base `.pill` rule was pruned as "unused" and `shared-primitives.css`
    // regenerated EMPTY — and `ListRow.vue`'s chips, which deliberately declare no visual properties because
    // ADR 0029 made the shared layer their single definition, rendered completely unstyled. The admission
    // test ADR 0029 publishes is "a C# primitive emits it AND a template-authored Vue component consumes it";
    // what actually gated carriage was IR usage, an unrelated condition that no consumer of this repo
    // controls. An allowlisted class is carried because it is NAMED, which is the entire point of an
    // allowlist — so it bypasses the prune. The bound stays tight: nothing enters by being used.
    const keep = selectors.filter(
      (s) => isSharedPrimitive(s) || isRuntimeBodyClass(s) || selectorIsUsed(s, used),
    )
    if (keep.length === 0) {
      stats.droppedUnused += 1
      return { scoped: null, shared: null, runtime: null }
    }

    // Partition BEFORE scoping: an unscoped selector is emitted verbatim, never nested under `.ir-content`.
    // Three-way, and the two unscoped allowlists are disjoint by construction (guard-tested), so the order of
    // these filters cannot change which layer claims a selector.
    const sharedSel = keep.filter(isSharedPrimitive)
    const runtimeSel = keep.filter(isRuntimeBodyClass)
    const scopedSel = keep.filter((s) => !isSharedPrimitive(s) && !isRuntimeBodyClass(s))

    let sharedText = null
    if (sharedSel.length > 0) {
      stats.sharedRules += 1
      sharedText = `${sharedSel.join(',\n')} {${block.body.replace(/\n+$/, '\n')}}`
      manifestSharedRules.push({
        selector: sharedSel.join(', '),
        carried: true,
        unscoped: true,
        ...(insideAt ? { within: insideAt } : {}),
      })
      // Recorded in the SCOPED layer's list too, as a handoff rather than a disappearance — otherwise a rule
      // that used to be enumerated for Story 23.4 would silently drop off the list it is retired from.
      manifestRules.push({
        selector: sharedSel.join(', '),
        carried: false,
        ...(insideAt ? { within: insideAt } : {}),
        reason: 'shared primitive — emitted UNSCOPED into shared-primitives.css; see sharedPrimitives below',
      })
    }

    let runtimeText = null
    if (runtimeSel.length > 0) {
      stats.runtimeRules += 1
      runtimeText = `${runtimeSel.join(',\n')} {${block.body.replace(/\n+$/, '\n')}}`
      manifestRuntimeRules.push({
        selector: runtimeSel.join(', '),
        carried: true,
        unscoped: true,
        ...(insideAt ? { within: insideAt } : {}),
      })
      // Same handoff bookkeeping as the shared layer: recorded in the SCOPED list as a move with a reason,
      // never as a silent disappearance.
      manifestRules.push({
        selector: runtimeSel.join(', '),
        carried: false,
        ...(insideAt ? { within: insideAt } : {}),
        reason: 'runtime body-level class — emitted UNSCOPED into runtime-body.css; see runtimeBodyClasses below',
      })
    }

    if (scopedSel.length === 0) return { scoped: null, shared: sharedText, runtime: runtimeText }

    const scoped = scopePrelude(scopedSel.join(','))
    if (!scoped) {
      // Every surviving selector addressed the document root and had no descendant to scope.
      stats.droppedRoot += 1
      manifestRules.push({
        selector: scopedSel.join(', '),
        carried: false,
        reason: 'root-level rule — no descendant to scope under .ir-content; see web/assets/base.css',
      })
      return { scoped: null, shared: sharedText, runtime: runtimeText }
    }
    stats.carriedRules += 1
    stats.carriedSelectors += scopedSel.length
    const placed = scopedSel.length + sharedSel.length + runtimeSel.length
    manifestRules.push({
      selector: scopedSel.join(', '),
      carried: true,
      ...(insideAt ? { within: insideAt } : {}),
      ...(placed < selectors.length
        ? {
            note:
              `${selectors.length - placed} unused selector(s) in the source rule ` +
              'were not carried',
          }
        : {}),
    })
    return {
      scoped: `${scoped} {${block.body.replace(/\n+$/, '\n')}}`,
      shared: sharedText,
      runtime: runtimeText,
    }
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
    const shared = []
    const runtime = []
    for (const block of level) {
      if (block.kind === 'statement') continue

      if (block.kind === 'rule') {
        const text = takeRule(block, at)
        if (text.scoped) out.push(text.scoped)
        if (text.shared) shared.push(text.shared)
        if (text.runtime) runtime.push(text.runtime)
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
        if (inner.out.length) out.push(`${prelude} {\n${inner.out.join('\n\n')}\n}`)
        // A shared primitive inside a conditional at-rule keeps its condition — dropping the wrapper would
        // apply a reduced-motion or narrow-viewport override unconditionally. Same for the runtime layer:
        // `.ss-tooltip` has reduced-motion and narrow-viewport overrides that must not apply unconditionally.
        if (inner.shared.length) shared.push(`${prelude} {\n${inner.shared.join('\n\n')}\n}`)
        if (inner.runtime.length) runtime.push(`${prelude} {\n${inner.runtime.join('\n\n')}\n}`)
        continue
      }
      // @font-face and friends: carried whole — they declare a resource, not a selector match.
      out.push(`${prelude} {${block.body}}`)
      manifestRules.push({ selector: prelude, carried: true })
    }
    return { out, shared, runtime }
  }

  const walked = walk(blocks, null)
  carried.push(...walked.out)
  sharedCarried.push(...walked.shared)
  runtimeCarried.push(...walked.runtime)

  // ── 3. Keyframes, only those the carried rules animate ─────────────────────────────────────────────────
  //
  // ALL THREE layers are scanned: an unscoped rule that animates would otherwise name a keyframe nobody
  // emitted, which fails silently — the rule applies, the animation does not, and no markup comparison can
  // see it. `.ss-tooltip` has a fade-in, so the runtime layer is not a hypothetical third case here.
  const body = [...carried, ...sharedCarried, ...runtimeCarried].join('\n')
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
    ' * The IR ships markup authored against that whole stylesheet; this app imports only the token',
    ' * bridge. This layer carries the rules the IR surfaces actually use, re-nested under',
    ` * \`${SCOPE}\` so they cannot reach a template-authored component.`,
    ' *',
    ' * ⚠️ STILL TRANSITIONAL, BUT NO LONGER "Story 23.4 retires it". Story 23.4 ATTEMPTED the retirement',
    ' * (owner decision D5) and could not complete it. Measured: only ~5% of the carried rules are prose and',
    ' * authorable today; ~95% style bespoke vocabulary INJECTED as rendered HTML across ~380 classes, which',
    ' * cannot be de-injected while ADR 0016 keeps rendered HTML in the IR and no per-family view models exist.',
    ' *',
    ' * What the residue waits on, per bucket (see ADR 0018 §Addendum and',
    ' * web/measurements/ir-content-residue.json, regenerate with `npm run report:ir-content-residue`):',
    ' *   chart / card / other  → EPIC 22: structured per-family + per-chart data in the IR',
    ' *   status                → the token bridge, so rules cannot drift from the six --status-* tokens',
    ' *   chrome                → NEVER EMPTIES. Owner decision D2 + ADR 0024 keep C# composing nav +',
    ' *                           wayfinding + <main> into the region permanently. These need a change of',
    ' *                           PROVENANCE (an owned sheet here), not deletion.',
    ' *',
    ' * Re-run `npm run extract:ir-content` after any change to the C# stylesheet or to what the migrated',
    ' * surfaces render. `npm run check:ir-content` fails the build when this file and the source diverge.',
    ' */',
  ].join('\n')

  const css = `${banner}\n\n${[...carried, ...keyframes].join('\n\n')}\n`

  // The unscoped sibling. Its own banner says plainly that it is NOT scoped, because that is the one property
  // a reader would otherwise assume it shares with `ir-content.css`. [ADR 0029]
  const sharedBanner = [
    '/* GENERATED FILE - DO NOT EDIT.',
    ` * Extracted from ${SOURCE_LABEL} by \`npm run extract:ir-content\` (Story 23.2 re-review; ADR 0029).`,
    ' *',
    ' * ⚠️ UNSCOPED, unlike its sibling ir-content.css. These are SHARED PRIMITIVE classes that a C# primitive',
    ' * emits and a template-authored Vue component consumes — `ListRow.Chip` emits `class="list-row-chip pill"`',
    " * and every visual property of that chip is `.pill`'s. A rule nested under `.ir-content` can only ever",
    ' * reach INJECTED markup, so the alternative was hand-retyping those declarations inside the SFC, which is',
    ' * what drifted before: serif instead of Courier, wrong padding, wrong tokens.',
    ' *',
    ' * BOUNDED by an explicit allowlist, not by usage: a rule is carried only when EVERY class it names is on',
    ` * the list. Today the whole list is: ${SHARED_PRIMITIVES.map((c) => `.${c}`).join(', ')}.`,
    ' * Growing it is an architectural decision — see ir-content-lib.mjs and ADR 0029 for the admission test.',
    ' *',
    ' * Each rule here is REMOVED from ir-content.css rather than duplicated into both, so the app has exactly',
    ' * one definition. An unscoped rule still matches inside .ir-content, so injected markup is unaffected.',
    ' *',
    ' * TRANSITIONAL: Story 23.4 retires this alongside ir-content.css. The manifest enumerates every rule.',
    ' */',
  ].join('\n')

  // An empty allowlist must still produce a valid, obviously-empty sheet rather than a banner with a stray
  // blank body — the file is imported unconditionally by nuxt.config.ts.
  const sharedCss = `${sharedBanner}\n\n${sharedCarried.join('\n\n')}\n`

  // The SECOND unscoped sibling. Kept as its own file rather than folded into shared-primitives.css so a
  // failure localizes to a named artifact and the two allowlists cannot be conflated in review — they answer
  // different questions and have different admission tests. [ADR 0033 §new-gate rule, ADR 0039]
  const runtimeBanner = [
    '/* GENERATED FILE - DO NOT EDIT.',
    ` * Extracted from ${SOURCE_LABEL} by \`npm run extract:ir-content\` (ADR 0039).`,
    ' *',
    ' * ⚠️ UNSCOPED, and for a DIFFERENT reason than shared-primitives.css. These classes are attached at',
    ' * RUNTIME by specscribe.js to a node it appends to document.body — the shared tooltip and the two rich',
    ' * cards rendered into it. That node is OUTSIDE the `.ir-content` wrapper, so `.ir-content .ss-tooltip`',
    ' * can never match it no matter what the harvest saw. Scoping these is not merely unhelpful, it is wrong.',
    ' *',
    ' * The body-level placement is deliberate: `.ss-tooltip` is position:absolute/z-index:300 so it layers',
    ' * above the sticky nav and clamps to the viewport instead of being clipped, and its coordinates are',
    ' * computed in PAGE space. Re-parenting it under `.ir-content` would trade a styling bug for a clipping',
    ' * one — see ADR 0039 for why that alternative was rejected.',
    ' *',
    ' * BOUNDED by an explicit allowlist, not by usage: a rule is carried only when EVERY class it names is on',
    ` * the list. Today the whole list is: ${RUNTIME_BODY_CLASSES.map((c) => `.${c}`).join(', ')}.`,
    ' * Admission test: is this class only ever applied to a node provably OUTSIDE `.ir-content`?',
    ' * "Runtime-applied" alone is NOT sufficient — the hierarchy explorer stamps sector, probe, swatch and',
    ' * breadcrumb classes at runtime too, and those live inside the chart panel, so they stay scoped and are',
    ' * seeded through CONDITIONAL_CLASSES instead.',
    ' *',
    ' * Each rule here is REMOVED from ir-content.css rather than duplicated into both, so the app has exactly',
    ' * one definition. The manifest records the handoff from both sides.',
    ' */',
  ].join('\n')

  const runtimeCss = `${runtimeBanner}\n\n${runtimeCarried.join('\n\n')}\n`

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
    transitional:
      'Story 23.4 attempted the retirement (owner decision D5) and could NOT complete it: ~95% of the ' +
      'carried rules style bespoke vocabulary injected as rendered HTML, which needs Epic 22 view models to ' +
      'de-inject, and the `chrome` bucket never empties because owner decision D2 + ADR 0024 keep C# ' +
      'composing the region permanently. See ADR 0018 §Addendum and ' +
      'web/measurements/ir-content-residue.json for the per-bucket blocker.',
    migratedFamilies: ['index.html', 'epics.html', 'epics/epic-{N}.html', 'epics/story-{id}.html'],
    stats: {
      carriedRules: stats.carriedRules,
      carriedSelectors: stats.carriedSelectors,
      carriedKeyframes: stats.carriedKeyframes,
      droppedRoot: stats.droppedRoot,
      generatedBytes: outBytes,
      sharedRules: stats.sharedRules,
      runtimeRules: stats.runtimeRules,
    },
    rules: manifestRules,
    /**
     * The UNSCOPED sibling layer. [ADR 0029]
     *
     * Enumerated here rather than in a second manifest so Story 23.4 retires ONE list, not two — and so the
     * handoff is visible from both sides: a rule that moved out of `rules` above appears there with
     * `carried: false` and a reason, and appears here with `unscoped: true`.
     *
     * `allowlist` is committed deliberately: it is a hand-authored constant, so it cannot move on its own,
     * and it is the whole boundary of the layer. `generatedBytes` obeys the same committed-fields rule as its
     * sibling above — it moves only when `shared-primitives.css` moves.
     */
    sharedPrimitives: {
      generatedFile: 'web/assets/shared-primitives.css',
      unscoped: true,
      allowlist: SHARED_PRIMITIVES,
      admission:
        'A class qualifies only if a C# primitive emits it AND a template-authored Vue component consumes '
        + 'it. Classes that appear only in injected markup are covered by the scoped layer and must not be '
        + 'added here.',
      stats: { rules: stats.sharedRules, generatedBytes: Buffer.byteLength(sharedCss) },
      rules: manifestSharedRules,
    },
    /**
     * The SECOND unscoped layer. [ADR 0039]
     *
     * Separate from `sharedPrimitives` rather than merged into it because the two answer different questions
     * and admit on different tests. Merging them would let a reviewer approve a `.pill`-shaped addition and
     * silently widen the tooltip escape hatch, or the reverse.
     */
    runtimeBodyClasses: {
      generatedFile: 'web/assets/runtime-body.css',
      unscoped: true,
      allowlist: RUNTIME_BODY_CLASSES,
      admission:
        'A class qualifies only if it is applied exclusively to a node specscribe.js attaches OUTSIDE the '
        + '.ir-content wrapper, so no scoped selector could ever match it. Being invisible to the harvest is '
        + 'NOT the test (codemap-card* is harvested and still belongs here); being runtime-applied is not '
        + 'sufficient either (the explorer\'s in-panel classes are seeded via CONDITIONAL_CLASSES instead).',
      stats: { rules: stats.runtimeRules, generatedBytes: Buffer.byteLength(runtimeCss) },
      rules: manifestRuntimeRules,
    },
  }

  return {
    css,
    sharedCss,
    runtimeCss,
    manifest,
    dropped,
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
