#!/usr/bin/env node
// `npm run check:a11y` — Story 23.3 AC #2, asserted over the EMITTED HTML.
//
// These are the structural conventions every SpecScribe page has carried since Stories 1.4/1.5 and 3.5, and
// they are exactly the ones a migration silently breaks: a shell that emits its own `<main>` on top of an
// injected one gives you two landmarks and a duplicate id, and nothing renders differently enough to notice.
//
// What is checked, and why each one is here rather than assumed:
//
//   1. exactly one `<main id="main-content">` per page      — trap #1 of this story, in one assertion
//   2. exactly one skip link, and it is the FIRST focusable  — UX-DR16; a skip link you tab to third is not one
//   3. `<html lang>` is present                              — Nuxt emits none by default
//   4. every status chip carries a WORD, not colour alone    — UX-DR17, including inside injected markup
//   5. reduced motion is neutralised globally, not per-rule  — the motion-token contract
//
// Run `npm run generate` first.

import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { assertFullRun, MEASUREMENTS_DIR, PUBLIC_DIR, readOrNull, walk } from './harness-lib.mjs'

assertFullRun('check:a11y')

let files
try {
  files = walk(PUBLIC_DIR)
} catch (err) {
  if (err.code === 'ENOENT') {
    console.error('check:a11y — .output/public not found. Run `npm run generate` first.')
    process.exit(1)
  }
  throw err
}

/**
 * Nitro's SPA-fallback shells. DECLARED exclusion, not a silent one.
 *
 * `200.html` and `404.html` are 263-byte static-host fallbacks Nitro emits for client-side routing — an
 * empty `<div id="__nuxt">` and nothing else. They are build artifacts rather than pages of this site: they
 * carry no `<main>`, no skip link and no `lang`, and adding an `error.vue` does not change them because
 * they are templates, not rendered routes.
 *
 * They are a real (small) gap on a DEPLOYED site — someone who mistypes a URL gets a blank page — and that
 * belongs to Story 23.5, which owns how this output is served. Recorded in the story record, counted below,
 * and deliberately not folded into this story's pass/fail.
 */
const FALLBACK_SHELLS = new Set(['200.html', '404.html'])

const allHtml = files.filter((f) => f.endsWith('.html'))
const pages = allHtml.filter((f) => !FALLBACK_SHELLS.has(f))
const excluded = allHtml.filter((f) => FALLBACK_SHELLS.has(f))
const failures = []
let statusChips = 0
const fail = (page, rule, detail) => failures.push({ page, rule, detail })

/** Elements that take focus without a tabindex. Enough to answer "what is focusable first?". */
const FOCUSABLE = /<(a\b[^>]*\shref|button\b|input\b|select\b|textarea\b|[a-z]+\b[^>]*\stabindex\s*=\s*"0")/i

for (const page of pages) {
  const html = readFileSync(join(PUBLIC_DIR, page), 'utf8')
  const body = html.slice(html.indexOf('<body'))

  // 1 — exactly one main landmark, and exactly one element with that id.
  const mains = body.match(/<main\b/gi) ?? []
  const landmarks = body.match(/id="main-content"/g) ?? []
  if (mains.length !== 1) fail(page, 'one-main', `${mains.length} <main> elements`)
  if (landmarks.length !== 1) fail(page, 'one-landmark-id', `${landmarks.length} id="main-content"`)

  // 2 — exactly one skip link, and nothing focusable before it.
  const skips = body.match(/href="#main-content"/g) ?? []
  if (skips.length !== 1) fail(page, 'one-skip-link', `${skips.length} links to #main-content`)
  const firstFocusable = FOCUSABLE.exec(body)
  const skipAt = body.indexOf('href="#main-content"')
  if (firstFocusable && skipAt >= 0) {
    const firstTagStart = firstFocusable.index
    const skipTagStart = body.lastIndexOf('<a', skipAt)
    if (firstTagStart < skipTagStart) {
      fail(page, 'skip-link-first', `something focusable precedes the skip link at byte ${firstTagStart}`)
    }
  }

  // 3 — a declared document language.
  if (!/<html[^>]*\slang\s*=/i.test(html)) fail(page, 'html-lang', 'no lang attribute on <html>')

  // 3b — the wayfinding band is one wrapper and it CLOSES before <main>.
  //
  // This rule exists because the bug it catches actually shipped into a build during this story: the region
  // splitter prepended a wrapper the migrated pages already had, so `<main>` and `<footer>` ended up nested
  // INSIDE the breadcrumb band on 187 pages. The `<main>` region stayed byte-identical, so parity, link
  // resolution and every other check here passed — it was visible only as real DOM geometry in a browser.
  // A structural assertion is cheaper than remembering to look.
  const wrappers = body.match(/<div class="page-wayfinding"/g) ?? []
  if (wrappers.length > 1) {
    fail(page, 'wayfinding-single', `${wrappers.length} .page-wayfinding wrappers (expected at most 1)`)
  }
  const wrapAt = body.indexOf('<div class="page-wayfinding"')
  const mainAt = body.indexOf('<main')
  if (wrapAt >= 0 && mainAt > wrapAt) {
    // Walk the band with a depth counter: it must return to zero before <main> begins.
    const band = body.slice(wrapAt, mainAt)
    const depth = (band.match(/<div\b/g) ?? []).length - (band.match(/<\/div>/g) ?? []).length
    if (depth !== 0) {
      fail(page, 'wayfinding-closed', `wayfinding band is ${depth > 0 ? 'unclosed' : 'over-closed'} before <main> (depth ${depth})`)
    }
  }

  // 4 — UX-DR17: no status signalled by colour alone. A status chip whose element renders no text is
  //     colour-only by definition. The chip classes are the portal's own (`StatusStyles`), listed rather
  //     than pattern-matched so a rule that stops matching anything is visible as a zero in the counts
  //     below instead of passing silently.
  for (const m of body.matchAll(
    /<span class="([^"]*\b(?:status-badge|epic-status|epic-remaining-status)\b[^"]*)"[^>]*>([\s\S]*?)<\/span>/g,
  )) {
    statusChips += 1
    const text = m[2].replace(/<[^>]*>/g, '').replace(/&[a-z]+;/g, ' ').trim()
    if (text === '') fail(page, 'status-word', `status chip with no word: class="${m[1]}"`)
  }
}

// 5 — the reduced-motion contract is global, declared once. Checked against the emitted stylesheets rather
//     than the sources, because that is what a browser actually receives.
const cssFiles = files.filter((f) => f.endsWith('.css'))
const allCss = cssFiles.map((f) => readOrNull(join(PUBLIC_DIR, f)) ?? '').join('\n')
const reduceBlocks = allCss.match(/@media\s*\(prefers-reduced-motion:\s*reduce\)/g) ?? []
if (reduceBlocks.length === 0) {
  fail('(stylesheets)', 'reduced-motion', 'no `prefers-reduced-motion: reduce` block in the emitted CSS')
}
// The reduce block has to neutralise INJECTED content too, which it can only do with a universal selector.
const universalReduce = /@media\s*\(prefers-reduced-motion:\s*reduce\)\s*\{[^}]*\*[^}]*\{/.test(
  allCss.replace(/\s+/g, ' '),
)
if (!universalReduce) {
  fail(
    '(stylesheets)',
    'reduced-motion-universal',
    'the reduce block does not use a universal selector, so it cannot reach v-html-injected content',
  )
}

const lines = []
const say = (s = '') => {
  lines.push(s)
  console.log(s)
}

const byRule = new Map()
for (const f of failures) byRule.set(f.rule, (byRule.get(f.rule) ?? 0) + 1)

say('')
say('Story 23.3 AC #2 — accessibility and motion conventions, over the emitted HTML')
say('')
say(`  pages checked        ${pages.length}`)
say(`  excluded             ${excluded.length}  ${excluded.join(', ')} — Nitro SPA-fallback shells,`)
say(`                       build artifacts rather than pages. Story 23.5 owns them (see the script).`)
say(`  stylesheets checked  ${cssFiles.length}`)
say(`  status chips seen    ${statusChips}`)
say(`  failures             ${failures.length}`)
say('')

if (failures.length === 0) {
  say('  one-main                every page has exactly one <main id="main-content">')
  say('  one-skip-link           every page has exactly one skip link, first among focusables')
  say('  html-lang               every page declares a document language')
  say('  status-word             no status badge is rendered as colour alone (UX-DR17)')
  say('  reduced-motion          a universal reduce block reaches injected content')
} else {
  for (const [rule, n] of byRule) say(`  ${rule}: ${n}`)
  say('')
  for (const f of failures.slice(0, 30)) say(`  ${f.page}\n    ${f.rule}: ${f.detail}`)
  if (failures.length > 30) say(`  … and ${failures.length - 30} more (all in measurements/a11y.json).`)
}
say('')

mkdirSync(MEASUREMENTS_DIR, { recursive: true })
writeFileSync(join(MEASUREMENTS_DIR, 'a11y.txt'), `${lines.join('\n')}\n`, 'utf8')
writeFileSync(
  join(MEASUREMENTS_DIR, 'a11y.json'),
  `${JSON.stringify(
    { generatedBy: 'web/scripts/check-a11y.mjs', pages: pages.length, failures },
    null,
    2,
  )}\n`,
  'utf8',
)
console.log('  wrote measurements/a11y.txt + measurements/a11y.json')
console.log('')

process.exit(failures.length > 0 ? 1 : 0)
